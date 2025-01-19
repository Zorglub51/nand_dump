using System;
using Renci.SshNet;
using static System.Net.Mime.MediaTypeNames;


public class NandUtils
{
    public static void Main(string[] args)
    {
        string host = "169.254.13.37";
        string username = "root";
        string password = "";
        string path = AppDomain.CurrentDomain.BaseDirectory;

        using (SshClient ssh = new SshClient(host, username, password))
        {
            ssh.Connect();
            DumpNAND(ssh, path, "mmcblk0boot0", 4111);
            DumpNAND(ssh, path, "mmcblk0boot1", 4111);
            DumpNAND(ssh, path, "mmcblk0p1", 1396736*512);
            DumpNAND(ssh, path, "mmcblk0p2", 16384*512);
            DumpNAND(ssh, path, "mmcblk0p3", 67);
            DumpNAND(ssh, path, "mmcblk0p4", 0);
            DumpNAND(ssh, path, "mmcblk0p5", 4096*512);
            DumpNAND(ssh, path, "mmcblk0p6", 16384*512);
            DumpNAND(ssh, path, "mmcblk0p7", 204800*512);
            DumpNAND(ssh, path, "mmcblk0p8", 1376256*512);
            DumpNAND(ssh, path, "mmcblk0p9", 1376256* 512);
            DumpNAND(ssh, path, "mmcblk0p10", 8192*512);
        }
    }

    private static void DumpNAND(SshClient ssh, string path, string nandName, uint size)
    {
        if (File.Exists(Path.Combine(path, $@"{nandName}.gz")))
        {
            Console.WriteLine($"{nandName} already exists. Skipping...");
            return;
        }
        Console.WriteLine($"Backing up {nandName}");

        // ASCII animation to show that time is passing
        string[] animation = new string[] { "|", "/", "-", "\\" };
        int animationIndex = 0;

        using (var fileStream = new FileStream(Path.Combine(path, $@"{nandName}.gz.part"), FileMode.Create, FileAccess.Write))
        {
            uint totalBytesRead = 0;
            byte[] buffer = new byte[1024 * 1024]; // 1 MB buffer

            while (totalBytesRead < size)
            {
                uint bytesToRead = Math.Min(1024 * 1024, size - totalBytesRead);
                SshCommand command = ssh.CreateCommand($"cd /dev;dd if={nandName} bs=1M count=1 skip={totalBytesRead / (1024 * 1024)}");

                var result = command.BeginExecute();
                var outputStream = command.OutputStream;

                while (!result.IsCompleted)
                {
                    double progress = (double)totalBytesRead * 100 / size;
                    Console.Write($"\rEstimated progress: {progress:F1}% (may not reach or exceed 100%)");
                    Console.Write($"\r{animation[animationIndex++ % animation.Length]}");
                    Thread.Sleep(500);
                }

                int bytesRead = outputStream.Read(buffer, 0, (int)bytesToRead);
                fileStream.Write(buffer, 0, bytesRead);
                totalBytesRead += (uint)bytesRead;
            }
        }
        Console.WriteLine();

        File.Move(Path.Combine(path, $@"{nandName}.gz.part"), Path.Combine(path, $@"{nandName}.gz"));

        /*
        Console.WriteLine($"Backing up {nandName}");
        //SshCommand command = ssh.CreateCommand($"cd /dev;gzip -c {nandName}");
        SshCommand command = ssh.CreateCommand($"cd /dev;dd if={nandName}");

        // ASCII animation to show that time is passing
        string[] animation = new string[] { "|", "/", "-", "\\" };
        int animationIndex = 0;

        var result = command.BeginExecute();
        var outputStream = command.OutputStream;
        while (!result.IsCompleted)
        {
            double progress = (double)outputStream.Length * 100 / size; 
            Console.Write($"\rEstimated progress: {progress:F1}% (may not reach or exceed 100%)");
            Console.Write($"\r{animation[animationIndex++ % animation.Length]}");
            Thread.Sleep(500);
        }
        Console.WriteLine();

        byte[] data = new byte[command.OutputStream.Length];
        command.OutputStream.Read(data, 0, data.Length);
        File.WriteAllBytes(Path.Combine(path, $@"{nandName}.gz.part"), data);
        File.Move(Path.Combine(path, $@"{nandName}.gz.part"), Path.Combine(path, $@"{nandName}.gz"));
        */
    }
}