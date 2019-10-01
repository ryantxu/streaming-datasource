using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Timers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grafana.Web;
using NLog;
using Fleck;

using Hid = SharpLib.Hid;
using Win32 = SharpLib.Win32;

namespace HIDServer
{
    public class TheSockets
    {
        public List<IWebSocketConnection> open = new List<IWebSocketConnection>();

        public InfluxWriter influx;

        public void write(DeviceValue value)
        {
            string json = value.ToJSON();
            foreach(IWebSocketConnection socket in this.open) {
                socket.Send(json);
            }
            
            influx.writeLine(value.ToInfluxLine());
        }

        public void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Console.WriteLine("TICK "+DateTime.Now);
            influx.flush();
        }

    }

    class Program
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();



        static void Main(string[] args)
        {
            logger.Info("Starting!");

            TheSockets sockets = new TheSockets();
            SimpleWebServer nws = new SimpleWebServer(4567);
            nws.Start();

            InfluxConnection c = new InfluxConnection();
            c.URL = "http://localhost:8086/";

            HttpInfluxDB db = new HttpInfluxDB(c);
            sockets.influx = new HttpInfluxWriter(db, "influxdays", 50000);

            // Every 10s flush the data
            var timer = new Timer(10000);
            timer.Elapsed += new ElapsedEventHandler(sockets._timer_Elapsed);
            timer.Start();

            foreach (var d in HidSharp.DeviceList.Local.GetHidDevices())
            {
                logger.Info("DEVICE: " + d.DevicePath);
                try
                {
                    logger.Info(" > " + d.GetFriendlyName());
                }
                catch { }
            }


            DeviceWatcher watcherSide = null;
            DeviceWatcher watcherWheel = null;

            HidSharp.HidDevice dev = DeviceWatcher.find("Saitek Side Panel Control Deck");
            if (dev != null)
            {
                watcherSide = new DeviceWatcher("panel_", dev, sockets);
            }

            dev = DeviceWatcher.find("Saitek Heavy Eqpt. Wheel & Pedal");
            if (dev != null)
            {
                watcherWheel = new DeviceWatcher("wheel_", dev, sockets);
            }

            WebSocketServer server = new WebSocketServer("ws://0.0.0.0:8181");
            server.RestartAfterListenError = true;
            server.Start(socket =>
            {
                socket.OnOpen = () => {
                    sockets.open.Add(socket);
                    logger.Info("WebSocket Open! " +sockets.open.Count);

                    if(watcherSide!=null)
                    {
                        watcherSide.broadcast();
                    }
                    if (watcherWheel != null)
                    {
                        watcherWheel.broadcast();
                    }

                };
                socket.OnClose = () => { sockets.open.Remove(socket); logger.Info("WebSocket Remove! " + sockets.open.Count); };
                socket.OnMessage = message => logger.Info("WebSocket: "+message);
            });
            

            while (true)
            {
                var x = Console.ReadLine();
                Console.WriteLine("ECHO: "+x);
            }
        }
    }
}
