using Tftp.Net;


public class NandUtils
{
    static string host = "169.254.13.37";
    static string username = "root";
    static string password = "";
    static long currentExpectedSize = 0;

    private static AutoResetEvent TransferFinishedEvent = new AutoResetEvent(false);

    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("usage : nand_dump.exe [command]");
            Console.WriteLine("     [command]");
            Console.WriteLine("         full : dumps a single file for the entire nand, including all partitions");
            Console.WriteLine("         split : dumps inidividual files for partitions 1, 2, 5, 6 (kernel), 7 (filsystem), 8 (game save states), 9 (emulator and games) and 10");
            Console.WriteLine("         restore N : restore partition N if mmcblk0pN is available");
            Console.WriteLine("         -bstart=starting block -bcount=number of blocks to dump -bsize=block size (512, 1024, 2048, 4096, 8192)");

            return;
        }

        string path = AppDomain.CurrentDomain.BaseDirectory;

        if (args[0].ToLower() == "full")
        {
            DownloadFile("/dev/mmcblk0", Path.Combine(path, "full_nand.bin"), "full nand", 3909091328);
        }
        else if (args[0].ToLower() == "split")
        {
            DownloadFile("/dev/mmcblk0p1", Path.Combine(path, "mmcblk0p1"), "partition 1", 1347584 * 512);
            DownloadFile("/dev/mmcblk0p2", Path.Combine(path, "mmcblk0p2"), "partition 2", 16384 * 512);
            DownloadFile("/dev/mmcblk0p5", Path.Combine(path, "mmcblk0p5"), "partition 5", 4096 * 512);
            DownloadFile("/dev/mmcblk0p6", Path.Combine(path, "mmcblk0p6"), "partition 6", 16384 * 512);
            DownloadFile("/dev/mmcblk0p7", Path.Combine(path, "mmcblk0p7"), "partition 7", 204800 * 512);
            DownloadFile("/dev/mmcblk0p8", Path.Combine(path, "mmcblk0p8"), "partition 8", 1376256 * 512);
            DownloadFile("/dev/mmcblk0p9", Path.Combine(path, "mmcblk0p9"), "partition 9", 4587520L * 512);
            DownloadFile("/dev/mmcblk0p10", Path.Combine(path, "mmcblk0p10"), "partition 10", 8192 * 512);
        }
        else if (args[0].ToLower() == "restore")
        {
            if (args.Length < 2)
            {
                Console.WriteLine("usage : nand_dump.exe restore N");
                Console.WriteLine("     N : partition number to restore");
                return;
            }
            string partition = "/dev/mmcblk0p" + args[1];
            string file = Path.Combine(path, "mmcblk0p" + args[1]);
            if (File.Exists(file))
            {
                Console.WriteLine("Restoring " + partition + " from " + file);
                UploadFile(partition, file, "partition " + args[1]);
            }
            else
            {
                Console.WriteLine("File " + file + " not found");
            }
        }
    }

    private static void UploadFile(string partition, string file, string description)
    {
        Console.WriteLine("Uploading " + description + " (" + file + ") to " + partition + "...");

        // Setup a TftpClient instance
        var client = new TftpClient(host);

        // Prepare a simple transfer
        ITftpTransfer transfer = client.Upload(partition);
        transfer.TransferMode = TftpTransferMode.octet;

        transfer.BlockSize = 512 * 16; // Adjust block size if necessary

        // Capture the events that may happen during the transfer
        transfer.OnProgress += new TftpProgressHandler(transfer_OnProgress);
        transfer.OnFinished += new TftpEventHandler(transfer_OnFinshed);
        transfer.OnError += new TftpErrorHandler(transfer_OnError);

        // Start the transfer and read the data that we're uploading from a file stream
        Stream stream = new FileStream(file, FileMode.Open, FileAccess.Read);
        currentExpectedSize = stream.Length;

        transfer.Start(stream);

        // Wait for the transfer to finish
        TransferFinishedEvent.WaitOne();
    }

    public static void DownloadFile(string remoteFile, string localFile, string description, long expectedSize)
    {
        currentExpectedSize = expectedSize;

        Console.WriteLine("Dumping " + description + " (" + remoteFile + ") to " + localFile + "...");
        //Setup a TftpClient instance
        var client = new TftpClient(host);

        //Prepare a simple transfer
        ITftpTransfer transfer = client.Download(remoteFile);
        transfer.TransferMode = TftpTransferMode.octet;

        transfer.BlockSize = 512 * 16; // Ajustez la taille de bloc si nécessaire

        //Capture the events that may happen during the transfer
        transfer.OnProgress += new TftpProgressHandler(transfer_OnProgress);
        transfer.OnFinished += new TftpEventHandler(transfer_OnFinshed);
        transfer.OnError += new TftpErrorHandler(transfer_OnError);

        //Start the transfer and write the data that we're downloading into a file stream
        Stream stream = new FileStream(localFile, FileMode.Create, FileAccess.Write);
        transfer.Start(stream);
        
        //Wait for the transfer to finish
        TransferFinishedEvent.WaitOne();
        //Console.ReadKey();
    }

    static void transfer_OnProgress(ITftpTransfer transfer, TftpTransferProgress progress)
    {
        Console.Write("\rTransfer running. Progress: " + progress.TransferredBytes * 100 / currentExpectedSize + "%" + " (" + progress.TransferredBytes / (1024 * 1024) + "MB / " + currentExpectedSize / (1024 * 1024) + " MB)");
    }

    static void transfer_OnError(ITftpTransfer transfer, TftpTransferError error)
    {
        Console.WriteLine("Transfer failed: " + error);
        TransferFinishedEvent.Set();
    }

    static void transfer_OnFinshed(ITftpTransfer transfer)
    {
        Console.WriteLine("\nTransfer succeeded.");
        TransferFinishedEvent.Set();
    }

    }