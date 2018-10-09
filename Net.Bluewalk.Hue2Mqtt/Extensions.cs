using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Q42.HueApi;

namespace Net.Bluewalk.Hue2Mqtt
{
    public static class Extensions
    {
        public static void FromState(this LightCommand value, State state)
        {
            if (value == null) return;

            value.Hue = state.Hue;
            value.Alert = state.Alert;
            value.Brightness = state.Brightness;
            value.ColorCoordinates = state.ColorCoordinates;
            value.ColorTemperature = state.ColorTemperature;
            value.Effect = state.Effect;
            value.On = state.On;
            value.Saturation = state.Saturation;
            value.TransitionTime = state.TransitionTime;
        }
    }
}
