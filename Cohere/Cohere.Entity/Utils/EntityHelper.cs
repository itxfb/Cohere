using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Cohere.Entity.Utils
{
    public static class EntityHelper
    {
        private static string _encryptionKey;

        public static string EncryptionKey
        {
            get => _encryptionKey;
            set => _encryptionKey = value ?? throw new Exception("Can't use encryption key. Not provided.");
        }

        public static EncryptionInfo Encrypt(string clearText)
        {
            var clearBytes = Encoding.Unicode.GetBytes(clearText);
            var salt = CreateSalt(13);
            using (var encryptor = Aes.Create())
            {
                var pdb = new Rfc2898DeriveBytes(_encryptionKey, salt);
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);

                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.Close();
                    }

                    clearText = Convert.ToBase64String(ms.ToArray());
                }
            }

            return new EncryptionInfo { EncryptedPassword = clearText, EncryptionSaltString = Convert.ToBase64String(salt) };
        }

        public static string Decrypt(string cipherText, string saltString)
        {
            cipherText = cipherText.Replace(" ", "+");
            var cipherBytes = Convert.FromBase64String(cipherText);

            using (var encryptor = Aes.Create())
            {
                var pdb = new Rfc2898DeriveBytes(_encryptionKey, Convert.FromBase64String(saltString));
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);

                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherBytes, 0, cipherBytes.Length);

                        cs.Close();
                    }

                    cipherText = Encoding.Unicode.GetString(ms.ToArray());
                }
            }

            return cipherText;
        }

        private static RandomNumberGenerator rng = RandomNumberGenerator.Create();
        private static byte[] CreateSalt(int size)
        {
            //Generate a cryptographic random number.
            var buff = new byte[size];
            rng.GetBytes(buff);

            return buff;
        }
    }
}