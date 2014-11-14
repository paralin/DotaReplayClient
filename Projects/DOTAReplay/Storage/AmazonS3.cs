using System.IO;
using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using DOTAReplay.Properties;

namespace DOTAReplay.Storage
{
    public static class AmazonS3
    {
        private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static AmazonS3Client Client;

        static AmazonS3()
        {
            Client = new AmazonS3Client(Settings.Default.AWSAccessID, Settings.Default.AWSAccessKey, RegionEndpoint.USEast1);
        }

        public static bool UploadFile(string path)
        {
            try
            {
                TransferUtility fileTransferUtility = new
                    TransferUtility(Client);

                fileTransferUtility.Upload(path, Settings.Default.BucketName);
                log.Debug("Uploaded to S3 "+Path.GetFileName(path));
                return true;
            }
            catch (AmazonS3Exception s3Exception)
            {
                log.Error("Problem uploading replay to S3.", s3Exception);
                return false;
            }
        } 
    }
}
