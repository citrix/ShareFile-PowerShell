using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ShareFile.Api.Powershell.Parallel
{
    class Utility
    {
        public static String GetMD5HashFromFile(Stream file)
        {
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] retVal = md5.ComputeHash(file);
            file.Close();
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < retVal.Length; i++)
            {
                sb.Append(retVal[i].ToString("x2"));
            }
            return sb.ToString();
        }

        public static String GetMD5HashFromFile(String fileName)
        {
            string fileHashCode = string.Empty;
            using (FileStream file = new FileStream(fileName, FileMode.Open))
            {

                fileHashCode = GetMD5HashFromFile(file);
                file.Close();
            }

            return fileHashCode;
        }
    }
}
