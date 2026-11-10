"""Erstellt eine Live-Kopie der vorhandenen Unity-Praxisszene, ohne sie umzubauen."""
import argparse
import hashlib
import pathlib
import re
import uuid

parser = argparse.ArgumentParser()
parser.add_argument('--unity-project', default=r'C:\Users\marle\arztdemo')
args = parser.parse_args()
project = pathlib.Path(args.unity_project)
source = project / 'Assets/Scenes/SampleScene.unity'
destination = project / 'Assets/Scenes/PraxisLive.unity'
if destination.exists():
    raise SystemExit('PraxisLive.unity existiert bereits; vorhandene Szene wird nicht ueberschrieben.')
original = source.read_bytes()
text = original.decode('utf-8-sig').replace('\r\n', '\n')
pattern = re.compile(r'^--- !u!(\d+) &(\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)', re.M | re.S)
blocks = {match.group(2): match.group(0) for match in pattern.finditer(text)}
spawner_guid = 'dfdacf81e27f29d4fa8bc09ea1dd209c'
spawners = [block for block in blocks.values() if spawner_guid in block]
if len(spawners) != 1:
    raise SystemExit('Genau ein vorhandener PatientSpawner wird zur automatischen Zuordnung benoetigt.')
spawner = spawners[0]

def reference(name):
    match = re.search(r'^  ' + re.escape(name) + r': (\{.*\})$', spawner, re.M)
    if not match:
        raise ValueError('Wegpunkt fehlt im Spawner: ' + name)
    return match.group(1)

def named_transform(name):
    objects = [key for key, block in blocks.items() if re.search(r'^  m_Name: ' + re.escape(name) + '$', block, re.M)]
    if len(objects) != 1:
        raise ValueError('Wegpunkt nicht eindeutig: ' + name)
    for key, block in blocks.items():
        if block.startswith('--- !u!4 ') and f'm_GameObject: {{fileID: {objects[0]}}}' in block:
            return '{fileID: ' + key + '}'
    raise ValueError('Transform fehlt: ' + name)

def script_guid(name):
    meta = project / ('Assets/Scripts/SimSharpLive/' + name + '.cs.meta')
    return re.search(r'^guid: (\w+)', meta.read_text(), re.M).group(1)

disabled = 0
for block in list(blocks.values()):
    if any(guid in block for guid in [spawner_guid, '4ae739768c75bb04db25272795045340', '7fa33472de98d32458833eccb217a509']):
        updated = block.replace('  m_Enabled: 1\n', '  m_Enabled: 0\n', 1)
        text = text.replace(block, updated, 1)
        disabled += 1

prefab = reference('patientPrefab')
prefab_id = re.search(r'fileID: (\d+)', prefab).group(1)
prefab_guid = re.search(r'guid: (\w+)', prefab).group(1)
for block in list(blocks.values()):
    if block.startswith('--- !u!1001 ') and f'm_SourcePrefab: {{fileID: 100100000, guid: {prefab_guid}' in block:
        override = f'''    - target: {{fileID: {prefab_id}, guid: {prefab_guid}, type: 3}}
      propertyPath: m_IsActive
      value: 0
      objectReference: {{fileID: 0}}
'''
        text = text.replace(block, block.replace('    m_Modifications:\n', '    m_Modifications:\n' + override, 1), 1)

ids = list(range(2800000001, 2800000006))
assert all(str(value) not in blocks for value in ids), 'FileID-Kollision'
go, transform, demo, connection, layout = ids
new = f'''--- !u!1 &{go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {transform}}}
  - component: {{fileID: {demo}}}
  - component: {{fileID: {connection}}}
  - component: {{fileID: {layout}}}
  m_Layer: 0
  m_Name: SimSharp Praxis Live
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{transform}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
'''

def component(file_id, class_name, fields):
    return f'''--- !u!114 &{file_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid(class_name)}, type: 3}}
  m_Name:
  m_EditorClassIdentifier: Assembly-CSharp::Arztpraxis.Live.{class_name}
{fields}'''

new += component(demo, 'SimSharpLiveDemo', f'  patientTemplate: {prefab}\n  praxisLayout: {{fileID: {layout}}}\n  simulationSpeed: 6\n')
new += component(connection, 'SimulationConnection', '  editorServerUrl: ws://localhost:5080/ws/simulation\n')
queue = next(block for block in blocks.values() if 'guid: 4ae739768c75bb04db25272795045340' in block)
queue_refs = re.search(r'  queuePositions:\n((?:  - .*\n)+)', queue).group(1)
fields = ''.join('  ' + key + ': ' + value + '\n' for key, value in [
    ('entrance', reference('entrancePoint')), ('reception', reference('receptionPoint')),
    ('waitingRoom', reference('waitingRoomPoint')), ('nurse', named_transform('NursePoint')),
    ('doctor', reference('doctorPoint')), ('exit', reference('exitPoint'))])
fields += '  receptionQueuePositions:\n' + queue_refs
fields += ''.join('  ' + key + ': ' + value + '\n' for key, value in [
    ('receptionDoor', reference('receptionDoor')), ('receptionThreshold', reference('receptionThreshold')),
    ('waitingRoomDoor', reference('waitingRoomDoor')), ('waitingRoomThreshold', reference('waitingRoomThreshold')),
    ('doctorDoor', reference('doctorDoor')), ('doctorThreshold', reference('doctorThreshold')),
    ('nurseDoor', named_transform('NurseDoor')), ('nurseThreshold', named_transform('NurseThreshold'))])
fields += '  upperDoorZOffset: -0.5\n'
new += component(layout, 'LivePraxisLayout', fields)
roots = '--- !u!1660057539 &9223372036854775807\n'
assert text.count(roots) == 1
text = text.replace(roots, new + roots, 1)
text += f'  - {{fileID: {transform}}}\n'
destination.write_text(text, encoding='utf-8', newline='\n')
destination.with_suffix('.unity.meta').write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n', encoding='utf-8')
assert source.read_bytes() == original
print('Erstellt:', destination)
print('Lokale Testkomponenten deaktiviert:', disabled)
print('Original unveraendert, SHA256:', hashlib.sha256(original).hexdigest())
