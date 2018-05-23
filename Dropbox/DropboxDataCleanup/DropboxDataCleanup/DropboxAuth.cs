using Dropbox.Api;
using DropboxHelper;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DropboxDataCleanup
{
    public class DropboxAuth
    {
        private readonly static byte[] additionalEntropy = { 7, 2, 6, 5, 9 }; // Used to further encrypt authentication information, changing this will cause any currently stored login details on the client machine to be invalid
        private static string accessToken;
        private static string oAuth2State;
        private static string appKey = "5xpffww5qkvm3hu";
        private static Uri redirectUri = new Uri("https://localhost/authorise");
        private static string appName = "DataCleanupUtility";

        public static DropboxClient SetupClient()
        {
            string accessToken = GetAccessToken();

            try
            {
                DropboxClientConfig config = new DropboxClientConfig();
                config.HttpClient = new HttpClient();

                return new DropboxClient(accessToken, config);
            }
            catch (HttpException e)
            {
                string msg = "Exception reported from RPC layer";
                msg += string.Format("\n    Status code: {0}", e.StatusCode);
                msg += string.Format("\n    Message    : {0}", e.Message);
                if (e.RequestUri != null)
                {
                    msg += string.Format("\n    Request uri: {0}", e.RequestUri);
                }
                MessageBox.Show(msg);

                return null;
            }
        }

        public static string GetAccessToken()
        {
            if (UnsecureCreds(appName) != null)
            {
                return Encoding.UTF8.GetString(UnsecureCreds(appName));
            }
            else
            {
                AcquireNewOAuthToken();
                return accessToken;
            }
        }

        private static void StoreAccessToken()
        {
            SecureCreds(accessToken, appName);
        }

        private static void AcquireNewOAuthToken()
        {
            oAuth2State = Guid.NewGuid().ToString("N");
            Uri authorizeUri = DropboxOAuth2Helper.GetAuthorizeUri(OAuthResponseType.Token, appKey, redirectUri, state: oAuth2State);
            BrowserWindow browser = new BrowserWindow(authorizeUri, redirectUri, oAuth2State);
            browser.ShowDialog();
            while (!browser.haveResult)
            {
                if (!browser.IsActive)
                    return;
            }
            accessToken = browser.accessToken;
            StoreAccessToken();
        }

        /// <summary>
        /// Secures the user's credentials against the Windows user profile and stores them in the registry under HKCU
        /// </summary>
        /// <param name="apiToken">API token to be stored</param>
        /// <param name="id">Name for the key</param>
        private static void SecureCreds(string accessToken, string id)
        {
            byte[] utf8Creds = Encoding.UTF8.GetBytes(accessToken);

            byte[] securedCreds = null;

            // Encrypt credentials
            try
            {
                securedCreds = ProtectedData.Protect(utf8Creds, additionalEntropy, DataProtectionScope.CurrentUser);

                // Check if registry path exists
                if (CheckOrCreateRegPath())
                {
                    // Save encrypted key to registry
                    RegistryKey credsKey = Registry.CurrentUser.OpenSubKey(@"Software\Swiftpage Support\Dropbox Logins", true);
                    credsKey.SetValue(id, securedCreds);
                }
            }
            catch (CryptographicException error)
            {
                //TODO: MessageHandler.handleMessage(false, 3, error, "Encrypting Jenkins login credentials");
            }
        }

        /// <summary>
        /// Pulls stored credentials from registry and decrypts them
        /// </summary>
        /// <param name="id">Name for the key</param>
        /// <returns>Returns unsecured utf8-encrypted byte array representing stored credentials</returns>
        private static byte[] UnsecureCreds(string id)
        {
            // Check if registry path exists
            if (CheckOrCreateRegPath())
            {
                byte[] securedCreds = null;
                byte[] utf8Creds = null;

                // Get encrypted key from registry
                try
                {
                    RegistryKey credsKey = Registry.CurrentUser.OpenSubKey(@"Software\Swiftpage Support\Dropbox Logins", false);
                    securedCreds = (byte[])credsKey.GetValue(id);

                    // Un-encrypt credentials
                    try
                    {
                        utf8Creds = ProtectedData.Unprotect(securedCreds, additionalEntropy, DataProtectionScope.CurrentUser);
                    }
                    catch (CryptographicException error)
                    {
                        //TODO: MessageHandler.handleMessage(false, 3, error, "Decrypting stored Jenkins login credentials"); ;
                    }
                }
                catch (Exception error)
                {
                    //TODO: MessageHandler.handleMessage(false, 3, error, "Locating reg key to get Jenkins credentials");
                }

                return utf8Creds;
            }
            return null;
        }

        /// <summary>
        /// Verifies that the registry key to store credentials exists, and creates it if not
        /// </summary>
        /// <returns>true if key is now created and valid, false if not</returns>
        private static bool CheckOrCreateRegPath()
        {
            //TODO: MessageHandler.handleMessage(false, 6, null, "Verifying Jenkins Login registry key path");
            RegistryKey key = null;

            // Check if subkey "HKCU\Software\Swiftpage Support" exists
            key = Registry.CurrentUser.OpenSubKey(@"Software\Swiftpage Support", false);
            if (key == null)
            {
                //TODO: MessageHandler.handleMessage(false, 5, null, @"Creating registry key 'HKCU\Software\Swiftpage Support'");

                try
                {
                    key = Registry.CurrentUser.OpenSubKey(@"Software", true);
                    key.CreateSubKey("Swiftpage Support");
                }
                catch (Exception error)
                {
                    //TODO: MessageHandler.handleMessage(false, 3, error, @"Attempting to create registry key 'HKCU\Software\Swiftpage Support'");
                    return false;
                }
            }

            // Check if subkey HKCU\Software\Swiftpage Support\Dropbox Logins exists
            key = Registry.CurrentUser.OpenSubKey(@"Software\Swiftpage Support\Dropbox Logins", false);
            if (key == null)
            {
                //TODO: MessageHandler.handleMessage(false, 5, null, @"Creating registry key 'HKCU\Software\Swiftpage Support\Dropbox Logins'");

                try
                {
                    key = Registry.CurrentUser.OpenSubKey(@"Software\Swiftpage Support", true);
                    key.CreateSubKey("Dropbox Logins");
                }
                catch (Exception error)
                {
                    //TODO: MessageHandler.handleMessage(false, 3, error, @"Attempting to create registry key 'HKCU\Software\Swiftpage Support\Dropbox Logins'");
                    return false;
                }
            }

            // Confirm that full subkey exists
            key = Registry.CurrentUser.OpenSubKey(@"Software\Swiftpage Support\Dropbox Logins", false);
            if (key != null)
            {
                //TODO: MessageHandler.handleMessage(false, 6, null, "Login reg key exists");
                return true;
            }
            else
            {
                //TODO: MessageHandler.handleMessage(false, 3, null, @"Testing access to key HKCU\Swiftpage Support\Dropbox Logins");
                return false;
            }
        }
    }
}
