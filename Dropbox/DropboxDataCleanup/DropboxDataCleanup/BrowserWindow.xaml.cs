using Dropbox.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DropboxHelper
{
    /// <summary>
    /// Interaction logic for BrowserWindow.xaml
    /// </summary>
    public partial class BrowserWindow : Window
    {
        private Uri redirectUri;
        public bool haveResult = false;
        public string accessToken;
        private string oAuth2State;

        public BrowserWindow(Uri uri, Uri redirectUri, string oAuth2State)
        {
            this.redirectUri = redirectUri;
            this.oAuth2State = oAuth2State;
            InitializeComponent();
            Browser.Navigate(uri);
        }

        private void BrowserNavigating(object sender, NavigatingCancelEventArgs e)
        {
            // Shamelessly taken from this example: https://github.com/dropbox/dropbox-sdk-dotnet/blob/master/dropbox-sdk-dotnet/Examples/SimpleTest/LoginForm.xaml.cs
            if (!e.Uri.ToString().StartsWith(redirectUri.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                // we need to ignore all navigation that isn't to the redirect uri.
                haveResult = true;
                return;
            }

            try
            {
                OAuth2Response result = DropboxOAuth2Helper.ParseTokenFragment(e.Uri);
                if (result.State != oAuth2State)
                {
                    // The state in the response doesn't match the state in the request.
                    haveResult = true;
                    return;
                }

                accessToken = result.AccessToken;
            }
            catch (ArgumentException)
            {
                // There was an error in the URI passed to ParseTokenFragment
            }
            finally
            {
                e.Cancel = true;
            }

            haveResult = true;
            Close();
        }
    }
}
