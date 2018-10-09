using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.Interfaces;
using Q42.HueApi.Models;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace Net.Bluewalk.Hue2Mqtt
{
    public class Hue2MqttLogic
    {
        private readonly MqttClient _mqttClient;
        private readonly string _mqttRootTopic;
        private readonly ILocalHueClient _hueClient;
        private bool _disconnectOnPurpose;
        private readonly Timer _tmrReconnect = new Timer(15000);
        private readonly Timer _tmrPollHue = new Timer(1000);

        private List<Sensor> _hueSensors;
        private List<Light> _hueLights;
        private List<string> _mqttSubscribedTopics;

        public Hue2MqttLogic(string mqttHost, string mqttRootTopic, string hueBridgeAddress, string hueBridgeUsername)
        {
            _mqttClient = new MqttClient(mqttHost);
            _mqttClient.ConnectionClosed += (sender, args) =>
            {
                if (!_disconnectOnPurpose)
                    _tmrReconnect.Start();
            };
            _mqttClient.MqttMsgPublishReceived += MqttClientOnMqttMsgPublishReceived;
            _mqttRootTopic = mqttRootTopic;
            _tmrReconnect.Elapsed += (sender, args) => Start();

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
            _mqttSubscribedTopics = new List<string>();

            _hueLights?.ForEach(l =>
            {
                var topic = $"{_mqttRootTopic}/light/{l.Id}/state/set";
#if DEBUG
                topic = $"dev/{topic}";
#endif
                _mqttClient.Subscribe(new[] { topic }, new[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
                _mqttSubscribedTopics.Add(topic);
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

        public void Start()
        {
            _mqttClient.Connect($"BluewalkHue2Mqtt-{Environment.MachineName}");
            _disconnectOnPurpose = false;
            
            InitializeHue();
            InitializeMqttSubscriptions();

            _tmrPollHue.Start();
        }

        public void Stop()
        {
            _tmrPollHue.Stop();
            if (_mqttClient == null || !_mqttClient.IsConnected) return;

            _disconnectOnPurpose = true;
            _mqttClient.Disconnect();
        }

        private void Publish(string topic, object message, bool retain = true)
        {
            Publish(topic, JsonConvert.SerializeObject(message), retain);
        }

        private void Publish(string topic, string message, bool retain = true)
        {
            if (_mqttClient == null || !_mqttClient.IsConnected) return;
            topic = $"{_mqttRootTopic}/{topic}";
#if DEBUG
            topic = $"dev/{topic}";
#endif
            _mqttClient.Publish(topic, Encoding.ASCII.GetBytes(message), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, retain);
        }

        private bool CompareState(State state1, State state2)
        {
            return state1.On == state2.On && state1.Alert == state2.Alert &&
                   state1.Brightness == state2.Brightness &&
                   state1.ColorCoordinates[0] == state2.ColorCoordinates[0] &&
                   state1.ColorCoordinates[1] == state2.ColorCoordinates[1] &&
                   state1.ColorMode == state2.ColorMode &&
                   state1.ColorTemperature == state2.ColorTemperature &&
                   state1.Effect == state2.Effect && state1.Hue == state2.Hue &&
                   state1.Hue == state2.Hue && state1.IsReachable == state2.IsReachable &&
                   state1.Mode == state2.Mode && state1.Saturation == state2.Saturation &&
                   state1.TransitionTime == state2.TransitionTime;
        }
        
        private void MqttClientOnMqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            var topic = e.Topic.ToUpper().Split('/');
            var message = Encoding.ASCII.GetString(e.Message);
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
                            _hueClient.SendCommandAsync(command, new List<string> { topic[2] });
                            break;
                        default:
                            break;
                    }
                    break;
                default:
                    break;
            }
        }
    }
}
