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
        private SMBLibrary.Server.NameServer m_nameServer;

        public Server()
        {

            IPAddress serverAddress = IPAddress.Any;
            //Dns.GetHostAddresses(Dns.GetHostName())
            SMBTransportType transportType = SMBTransportType.DirectTCPTransport;
            

            INTLMAuthenticationProvider provider = new Win32UserCollection();
            //should probably do our own implementation of this that lets everyone through. 
            //Till then we've hacked it so everyone is a guest.

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

            m_server = new SMBLibrary.Server.SMBServer(shares, provider, serverAddress, transportType);
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
                string shareName = shareNode.Attributes["Name"].Value;
                string sharePath = shareNode.Attributes["Path"].Value;

                shares.Add(shareName, new string[] {"*"}, new string[] {}, 
                            new DirectoryFileSystem(sharePath));
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