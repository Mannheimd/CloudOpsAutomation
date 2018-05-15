using Dropbox.Api;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace DropboxDataCleanup
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

    }

    public static class ApplicationVariables
    {
        private static DropboxClient _dropboxClient;
        public static DropboxClient dropboxClient
        {
            get
            {
                if (_dropboxClient == null)
                {
                    _dropboxClient = DropboxAuth.SetupClient();
                }
                return _dropboxClient;
            }
        }
    }
}
