using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.Interfaces;
using Q42.HueApi.Models;

namespace Net.Bluewalk.Hue2Mqtt
{
    public class Hue2MqttLogic
    {
        private readonly IManagedMqttClient _mqttClient;
        private readonly string _mqttHost;
        private readonly int _mqttPort;
        private readonly string _mqttRootTopic;

        private readonly ILocalHueClient _hueClient;
        private readonly Timer _tmrPollHue = new Timer(1000);

        private List<Sensor> _hueSensors;
        private List<Light> _hueLights;

        public Hue2MqttLogic(string mqttHost, int mqttPort, string mqttRootTopic, string hueBridgeAddress, string hueBridgeUsername)
        {
            _mqttRootTopic = !string.IsNullOrEmpty(mqttRootTopic) ? mqttRootTopic : "hue";
            _mqttHost = mqttHost;
            _mqttPort = mqttPort;

            _mqttClient = new MqttFactory().CreateManagedMqttClient();
            _mqttClient.ApplicationMessageReceived += MqttClientOnApplicationMessageReceived;
            
            _hueClient = new LocalHueClient(hueBridgeAddress,
                hueBridgeUsername);

            _tmrPollHue.Elapsed += PollHue;
        }

        private void InitializeHue()
        {
            _hueSensors = _hueClient.GetSensorsAsync().Result?.ToList();
            _hueSensors?.ForEach(s => Publish($"sensor/{s.Id}/state", s.State));

            _hueLights = _hueClient.GetLightsAsync().Result?.ToList();
            _hueLights?.ForEach(l => Publish($"light/{l.Id}/state", l.State));
        }

        public void InitializeMqttSubscriptions()
        {
            _hueLights?.ForEach(l =>
            {
                var topic = $"{_mqttRootTopic}/light/{l.Id}/state/set";
#if DEBUG
                topic = $"dev/{topic}";
#endif
                SubscribeTopic(topic);
            });
        }

        private void PollHue(object sender, ElapsedEventArgs e)
        {
            _tmrPollHue.Stop();

            var sensors = _hueClient.GetSensorsAsync().Result?.ToList();
            sensors?.Where(s => _hueSensors.FirstOrDefault(hs => hs.Id.Equals(s.Id) && hs.State.Lastupdated.Equals(s.State.Lastupdated)) == null)
                .ToList()
                .ForEach(s => Publish($"sensor/{s.Id}/state", s.State));
            _hueSensors = sensors;

            var lights = _hueClient.GetLightsAsync().Result?.ToList();
            lights?.Where(l => _hueLights.FirstOrDefault(hl => hl.Id.Equals(l.Id) && CompareState(hl.State, l.State)) == null)
                .ToList()
                .ForEach(l => Publish($"light/{l.Id}/state", l.State));
            _hueLights = lights;

            _tmrPollHue.Start();
        }

        public async void Start()
        {
            var options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(new MqttClientOptionsBuilder()
                    .WithClientId($"BluewalkHue2Mqtt-{Environment.MachineName}")
                    .WithTcpServer(_mqttHost, _mqttPort))
                .Build();

            await _mqttClient.StartAsync(options);
            
            InitializeHue();
            InitializeMqttSubscriptions();

            _tmrPollHue.Start();
        }

        public async void Stop()
        {
            _tmrPollHue.Stop();
            if (_mqttClient == null || !_mqttClient.IsConnected) return;

            await _mqttClient.StopAsync();
        }

        private async void SubscribeTopic(string topic)
        {
            topic = $"{_mqttRootTopic}/{topic}";

#if DEBUG
            topic = $"dev/{topic}";
#endif
            await _mqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic(topic).Build());
        }

        private void Publish(string topic, object message, bool retain = true)
        {
            Publish(topic, JsonConvert.SerializeObject(message), retain);
        }

        private async void Publish(string topic, string message, bool retain = true)
        {
            if (_mqttClient == null || !_mqttClient.IsConnected) return;
            topic = $"{_mqttRootTopic}/{topic}";
#if DEBUG
            topic = $"dev/{topic}";
#endif

            var msg = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(message)
                .WithExactlyOnceQoS()
                .WithRetainFlag()
                .Build();

            await _mqttClient.PublishAsync(msg);
        }

        private bool CompareState(State state1, State state2)
        {
            return state1.On == state2.On && state1.Alert == state2.Alert &&
                   state1.Brightness == state2.Brightness &&
                   //state1.ColorCoordinates[0] == state2.ColorCoordinates[0] &&
                   //state1.ColorCoordinates[1] == state2.ColorCoordinates[1] &&
                   state1.ColorMode == state2.ColorMode &&
                   state1.ColorTemperature == state2.ColorTemperature &&
                   state1.Effect == state2.Effect && state1.Hue == state2.Hue &&
                   state1.Hue == state2.Hue && state1.IsReachable == state2.IsReachable &&
                   state1.Mode == state2.Mode && state1.Saturation == state2.Saturation &&
                   state1.TransitionTime == state2.TransitionTime;
        }
        
        private async void MqttClientOnApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            var topic = e.ApplicationMessage.Topic.ToUpper().Split('/');
            var message = e.ApplicationMessage.ConvertPayloadToString();
#if DEBUG
            // Remove first part "dev"
            topic = topic.Skip(1).ToArray();
#endif
            /**
             * Topic[0] = _rootTopic
             * Topic[1] = Type (eg Light)
             * Topic[2] = Id
             * Topic[3] = Data (eg State)
             * Topic[4] = Action (eg Set)
             */

            switch (topic[4])
            {
                case "SET":
                    switch (topic[1])
                    {
                        case "LIGHT":
                            var state = JsonConvert.DeserializeObject<State>(message);
                            var command = new LightCommand();
                            command.FromState(state);
                            await _hueClient.SendCommandAsync(command, new List<string> { topic[2] });
                            break;
                    }
                    break;
            }
        }
    }
}
