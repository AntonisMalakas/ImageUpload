using Google.Cloud.Vision.V1;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Drawing;

namespace Pixel.Core.EFile
{
    public class Helper : IHelper
    {
        public Helper()
        {
            String baseVar = AppDomain.CurrentDomain.BaseDirectory;
            //String pathToData_App = baseVar + "/App_Data/try-apis-bb2ecad3c4a4.json";
            String pathToData_App = baseVar + "/wwwroot/try-apis-09a01dd67858.json";
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", pathToData_App);
        }

        public string GetPolicyId(dynamic imageToAnalyze)
        {
            string policyIdFromImage;
            string policyIdToReturn;
            var base64String = imageToAnalyze.base64Value.ToString();
            string base64CleanedString = base64String.Replace("data:image/png;base64,", "");
            byte[] imageBytes = Convert.FromBase64String(base64CleanedString);

            //MemoryStream ms1 = new MemoryStream(imageBytes, 0, imageBytes.Length);
            //ms1.Write(imageBytes, 0, imageBytes.Length);

            var client = ImageAnnotatorClient.Create();
            var image = Image.FromBytes(imageBytes);

            var documentsFromImage = client.DetectDocumentText(image);

            string[] stringSeparators = new string[] { "\r\n" };
            var documentTextList = documentsFromImage.Text.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string textInPhoto in documentTextList)
            {
                if (textInPhoto.Contains("Policy ID"))
                {
                    policyIdFromImage = textInPhoto;
                    var temp = policyIdFromImage.Split(null);
                    policyIdToReturn = temp[2];
                    return policyIdToReturn;
                }
            }
            return null;
        }


    }
}
