# Hue2Mqtt
Hue2Mqtt is a Windows service which connects to your Hue bridge and relays statusses via MQTT.

## Installation
Extract the release build to a folder and run `Net.Bluewalk.Hue2Mqtt.Service.exe --install`
This will install the service

## Configuration
Edit the `Net.Bluewalk.Hue2Mqtt.Service.exe.config` file and set the following settings accordingly under 
```
<configuration>
  <appSettings>
    <add key="MQTT_Host" value="127.0.0.1" />
    <add key="MQTT_RootTopic" value="hue" />
    <add key="HueBridge_Address" value="x.x.x.x" />
    <add key="HueBridge_Username" value="myusername" />
  </appSettings>
  ```

| Configuration setting | Description |
|-|-|
| MQTT_Host | IP address / DNS of the MQTT broker |
| MQTT_RootTopic | This text will be prepended to the MQTT Topic `hue/#` |
| HueBridge_Address | The IP / DNS of the Hue bridge |
| HueBridge_Username | Username for the Hue bridge API, see below |

## Acquiring a Hue bridge username
(taken from https://www.developers.meethue.com/documentation/getting-started)

1. Browse with a webbrowser to `http://<bridge ip address>/debug/clip.html`
2. In `URL` enter `http://<bridge ip address>/api`
3. For body enter `{"devicetype":"my_hue_app#iphone peter"}` (change devicetype accordingly)
4. Click on the button `POST`
5. Go the your Hue bridge and press the link button
6. Go back to the webbrowser and click the `POST` button again
7. There will be a response indicating success and a username, this is the username that we require

## Starting/stopping
Go to services.msc to start/stop the `Bluewalk Hue2Mqtt` service or run `net start BluewalkHue2Mqtt` or `net stop BluewalkHue2Mqtt`

## Uninstall
1. Stop the service
2. Run `Net.Bluewalk.Hue2Mqtt.Service.exe --uninstall`
3. Delete files