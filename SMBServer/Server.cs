using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using SMBLibrary;
using SMBLibrary.Server;
using SMBLibrary.Server.Win32;
using System.Linq;
using Utilities;

namespace SMBServer
{
    public class Server
    {
        public const string SettingsFileName = "Settings.xml";
        private SMBLibrary.Server.SMBServer m_server;
        
        public Server()
        {

            IPAddress serverAddress = IPAddress.Any;
            //Dns.GetHostAddresses(Dns.GetHostName())
            SMBTransportType transportType = SMBTransportType.DirectTCPTransport;
            
            ShareCollection shares;
            try
            {
                shares = ReadShareSettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Cannot read {0}: {1}", SettingsFileName, ex.Message);
                return;
            }

            m_server = new SMBLibrary.Server.SMBServer(shares, serverAddress, transportType);
            m_server.Start();
            
        }

        private ShareCollection ReadShareSettings()
        {
            ShareCollection shares = new ShareCollection();
            string executableDirectory = Path.GetDirectoryName(Application.ExecutablePath) + "\\";
            XmlDocument document = GetXmlDocument(executableDirectory + SettingsFileName);
            XmlNode sharesNode = document.SelectSingleNode("Settings/Shares");

            foreach (XmlNode shareNode in sharesNode.ChildNodes)
            {
                if (shareNode.Name.Equals("DirectoryShare", StringComparison.OrdinalIgnoreCase))
                { 
                    string shareName = shareNode.Attributes["Name"].Value;
                    string sharePath = shareNode.Attributes["Path"].Value;

                    shares.Add(shareName, new string[] {"*"}, new string[] {}, 
                                new DirectoryFileSystem(sharePath));
                }
                else if (shareNode.Name.Equals("DropShare", StringComparison.OrdinalIgnoreCase))
                {
                     string shareName = shareNode.Attributes["Name"].Value;
                     string account = shareNode.Attributes["Account"].Value;
                     string key = shareNode.Attributes["Key"].Value;
                     shares.Add(shareName, new string[] { "*" }, new string[] { },
                                 new AzureFileSystem(account, key));
                }
                else 
                {
                    throw new Exception("invalid config " +  shareNode.Name);
                }
            }



            return shares;
        }

       
        public static XmlDocument GetXmlDocument(string path)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(path);
            return doc;
        }
    }
}