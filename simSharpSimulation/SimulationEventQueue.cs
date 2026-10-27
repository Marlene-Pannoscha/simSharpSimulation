using System;
using System.Threading.Channels;

namespace simSharpSimulation
{
    public sealed class SimulationEventQueue
    {
        private readonly Channel<SimulationLiveEvent> _channel =
            Channel.CreateUnbounded<SimulationLiveEvent>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                });

        public ChannelReader<SimulationLiveEvent> Reader =>
            _channel.Reader;

        public void Publish(SimulationLiveEvent simulationEvent)
        {
            if (!_channel.Writer.TryWrite(simulationEvent))
            {
                throw new InvalidOperationException(
                    "Simulationsereignis konnte nicht gespeichert werden.");
            }
        }
    }
}