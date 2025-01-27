using Tftp.Net;
using DiscUtils;
using DiscUtils.Ext;
using DiscUtils.Streams;
using DiscUtils.Iso9660;
using DiscUtils.Fat;
using System.Collections.Concurrent;


public class NandUtils
{
    static string host = "169.254.13.37";
    static long currentExpectedSize = 0;

    private static AutoResetEvent TransferFinishedEvent = new AutoResetEvent(false);

    private static readonly Dictionary<int, (string Path, long Size)> PartitionsPCE = new Dictionary<int, (string, long)>
    {
        { 1, ("/dev/mmcblk0p1", 1347584 * 512) },
        { 2, ("/dev/mmcblk0p2", 16384 * 512) },
        { 5, ("/dev/mmcblk0p5", 4096 * 512) },
        { 6, ("/dev/mmcblk0p6", 16384 * 512) },
        { 7, ("/dev/mmcblk0p7", 204800 * 512) },
        { 8, ("/dev/mmcblk0p8", 1376256 * 512) },
        { 9, ("/dev/mmcblk0p9", 4587520L * 512) },
        { 10, ("/dev/mmcblk0p10", 8192 * 512) }
    };

    public static void Main(string[] args)
    {
        // Get the base directory of the application
        string path = AppDomain.CurrentDomain.BaseDirectory;

        // Check if no arguments are provided
        if (args.Length == 0)
        {
            // Display usage instructions
            Console.WriteLine("usage : nand_dump.exe [command]");
            Console.WriteLine("     [command]");
            Console.WriteLine("         test : tests TFTP connection");
            Console.WriteLine("         full : dumps a single file for the entire nand, including all partitions");
            Console.WriteLine("         split : dumps inidividual files for partitions 1, 2, 5, 6 (kernel), 7 (filsystem), 8 (game save states), 9 (emulator and games) and 10");
            Console.WriteLine("         dump N file : dumps partition N to file");
            Console.WriteLine("         restore N file: restore partition N from file");
            Console.WriteLine("         fullrestore file: restore the entire nand from a given file");
            Console.WriteLine("         extract file destination: extract files from dumped partition 'file' to destination folder");
        }
        else if (args[0].ToLower() == "extract")
        {
            // Check if the correct number of arguments are provided
            if (args.Length < 3)
            {
                // Display usage instructions for extract command
                Console.WriteLine("usage : nand_dump.exe extract partition destination");
                Console.WriteLine("     file : dumped partition file to extract files from");
                Console.WriteLine("     destination : path to the destination folder");
                return;
            }
            else
            {
                string filename = args[1];
                string destination = args[2];
                extract(filename, destination);
            }
        }
        else if (args[0].ToLower() == "dump")
        {
            // Check if the correct number of arguments are provided
            if (args.Length < 3)
            {
                // Display usage instructions for extract command
                Console.WriteLine("usage : nand_dump.exe dump N file");
                Console.WriteLine("     N : number of the partition to dump");
                Console.WriteLine("     file : file path & name for the dump");
                return;
            }
            else
            {
                string partitionNumber = args[1];
                string filename = args[2];
                if (PartitionsPCE.ContainsKey(int.Parse(partitionNumber)))
                {
                    DownloadFile(PartitionsPCE[int.Parse(partitionNumber)].Path, Path.Combine(path, filename), $"partition {partitionNumber}", PartitionsPCE[int.Parse(partitionNumber)].Size);
                }
                else
                {
                    Console.WriteLine("Partition number not found");
                }
            }
        }
        // Handle the "test" command 
        else if (args[0].ToLower() == "test")
        {
            // Setup a TftpClient instance
            try
            {
                TftpClient client = new TftpClient(host);
                Stream stream = new FileStream("tmp.txt", FileMode.Create, FileAccess.Write);
                client.Upload("/dev/null").Start(stream);
                Console.WriteLine("OK");
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine("KO");
                return;
            }
        }
        // Handle the "full" command to dump the entire NAND
        else if (args[0].ToLower() == "full")
        {
            // Download the entire NAND to a single file
            DownloadFile("/dev/mmcblk0", Path.Combine(path, "full_nand.bin"), "full nand", 3909091328);
        }
        // Handle the "split" command to dump individual partitions
        else if (args[0].ToLower() == "split")
        {
            // Loop through each partition and download it to a separate file
            foreach (var partition in PartitionsPCE)
            {
                DownloadFile(partition.Value.Path, Path.Combine(path, $"mmcblk0p{partition.Key}"), $"partition {partition.Key}", partition.Value.Size);
            }
        }
        // Handle the "restore" command to restore a specific partition
        else if (args[0].ToLower() == "restore")
        {
            // Check if the correct number of arguments are provided
            if (args.Length < 3)
            {
                // Display usage instructions for restore command
                Console.WriteLine("usage : nand_dump.exe restore N file");
                Console.WriteLine("     N : partition number to restore");
                Console.WriteLine("     file : path to the file to restore from");
                return;
            }
            int partitionNumber = int.Parse(args[1]);
            string file = args[2];
            // Check if the file exists
            if (File.Exists(file))
            {
                long fileSize = new FileInfo(file).Length;
                // Check if the file size matches the partition size
                if (PartitionsPCE.ContainsKey(partitionNumber) && PartitionsPCE[partitionNumber].Size == fileSize)
                {
                    string partition = PartitionsPCE[partitionNumber].Path;
                    Console.WriteLine("Restoring " + partition + " from " + file);
                    // Upload the file to the specified partition
                    UploadFile(partition, file, "partition " + partitionNumber);
                }
                else
                {
                    Console.WriteLine("File size does not match the partition size.");
                }
            }
            else
            {
                Console.WriteLine("File " + file + " not found");
            }
        }
        // Handle the "fullrestore" command to restore the entire NAND
        else if (args[0].ToLower() == "fullrestore")
        {
            // Check if the correct number of arguments are provided
            if (args.Length < 2)
            {
                // Display usage instructions for fullrestore command
                Console.WriteLine("usage : nand_dump.exe fullrestore file");
                Console.WriteLine("     file : path to the file to restore from");
                return;
            }
            string file = args[1];
            // Check if the file exists
            if (File.Exists(file))
            {
                long fileSize = new FileInfo(file).Length;
                // Check if the file size matches the full NAND size
                if (fileSize == 3909091328)
                {
                    Console.WriteLine("Restoring full nand from " + file);
                    // Upload the file to the entire NAND
                    UploadFile("/dev/mmcblk0", file, "full nand");
                }
                else
                {
                    Console.WriteLine("File size does not match the full nand size.");
                }
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
        TftpClient client = new TftpClient(host);
     

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

    static void extract(string filename, string destination)
    {
        // Check if the destination directory exists, if not, create it
        if (!Directory.Exists(destination))
        {
            Directory.CreateDirectory(destination);
        }

        // Open the ext2 file system
        using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
        using (ExtFileSystem extFs = new ExtFileSystem(fs))
        {
            ExtractDirectory(extFs, "", destination);
        }
    }

    // helper function to extract files from a directory - for recursive extraction
    static void ExtractDirectory(ExtFileSystem fileSystem, string sourcePath, string destinationPath)
    {
        // Créer le répertoire destination s'il n'existe pas
        Directory.CreateDirectory(destinationPath);

        foreach (var entry in fileSystem.GetFileSystemEntries(sourcePath))
        {
            string entryName = Path.GetFileName(entry);
            string entryDestinationPath = Path.Combine(destinationPath, entryName);

            var attributes = fileSystem.GetAttributes(entry);

            if ((attributes & FileAttributes.Directory) != 0)
            {
                Console.WriteLine($"Entering directory: {entry}");

                // Récursion pour les sous-répertoires
                ExtractDirectory(fileSystem, entry, entryDestinationPath);
            }
            else if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                Console.WriteLine($"Skipping symlink: {entry}");
            }
            else
            {
                Console.WriteLine($"Extracting file: {entry}");

                using (var sourceStream = fileSystem.OpenFile(entry, FileMode.Open))
                using (var destinationStream = File.Create(entryDestinationPath))
                {
                    sourceStream.CopyTo(destinationStream);
                }
            }
        }
    }
}
