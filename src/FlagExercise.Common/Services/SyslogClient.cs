using System.Net.Sockets;
using System.Text;

namespace FlagExercise.Common.Services;

public class SyslogClient
{
    public enum Severity { Emergency=0, Alert=1, Critical=2, Error=3, Warning=4, Notice=5, Info=6, Debug=7 }
    public enum Facility { User = 1, Daemon = 3, Local0 = 16 }

    public void Send(string host, int port, string app, string message, Severity sev = Severity.Info,
        Facility fac = Facility.Local0)
    {
        var pri = ((int)fac * 8) + (int)sev;
        var ts = DateTime.Now.ToString("MMM dd HH:mm:ss",
            System.Globalization.CultureInfo.InvariantCulture);
        var hostname = Environment.MachineName;
        var packet = $"<{pri}>{ts} {hostname} {app}: {message}";
        var bytes = Encoding.ASCII.GetBytes(packet);
        using var udp = new UdpClient();
        udp.Send(bytes, bytes.Length, host, port);
    }
}
