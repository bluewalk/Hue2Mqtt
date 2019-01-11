using System.Configuration;
using System.Configuration.Install;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using Net.Bluewalk.Hue2Mqtt;

namespace Net.Bluewalk.Hue2Mqtt.Service
{
    public partial class Service : ServiceBase
    {
        private Hue2MqttLogic _logic;

        public Service()
        {
            InitializeComponent();
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        // The main entry point for the process
        static void Main(params string[] args)
        {

#if (!DEBUG)
            if (System.Environment.UserInteractive)
            {
                var parameter = string.Concat(args);

                var svc = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == "BluewalkHue2Mqtt");

                switch (parameter)
                {
                    case "--install":
                        if (svc == null)
                            ManagedInstallerClass.InstallHelper(new[] { "/LogFile=", Assembly.GetExecutingAssembly().Location });
                        break;
                    case "--uninstall":
                        if (svc != null)
                            ManagedInstallerClass.InstallHelper(new[] { "/u", "/LogFile=", Assembly.GetExecutingAssembly().Location });
                        break;
                    case "--start":
                        if (svc != null)
                            if (svc.Status == ServiceControllerStatus.Stopped)
                                svc.Start();
                        break;
                    case "--stop":
                        if (svc?.Status == ServiceControllerStatus.Running)
                            svc.Stop();
                        break;
                    case "--pause":
                        if (svc?.Status == ServiceControllerStatus.Running)
                            svc.Pause();
                        break;
                    case "--continue":
                        if (svc?.Status == ServiceControllerStatus.Paused)
                            svc.Continue();
                        break;
                }
            }
            else
                Run(new Service());
#else
            var service = new Service();
            service.OnStart(args);

            System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
#endif
        }

        protected override void OnStart(string[] args)
        {
            if (!int.TryParse(ConfigurationManager.AppSettings["MQTT_Port"], out var port))
                port = 1833;

            _logic = new Hue2MqttLogic(
                ConfigurationManager.AppSettings["MQTT_Host"],
                port,
                ConfigurationManager.AppSettings["MQTT_RootTopic"],
                ConfigurationManager.AppSettings["HueBridge_Address"],
                ConfigurationManager.AppSettings["HueBridge_Username"]
            );

            _logic.Start();
        }

        protected override void OnStop()
        {
            _logic.Stop();
        }
    }
}
