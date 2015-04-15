/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Utilities;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace SMBServer
{
    public class Manifest
    {
        public DateTime time { get { return DateTime.Now; } }
        public ManifestFile Find(string path) { return null; }
        public IEnumerable<ManifestFile> Where(string path) { return null; }
    }

    public class ManifestFile
    {
        public ulong Length { get { return 0; } }
        public string Name { get { return "foobar"; } }
        public bool isDirectory { get { return false;  } }

        public string Hash { get { return "DEADBEEF"; }  }
    }

    public class AzureFileSystem : FileSystem
    {
        private CloudBlobContainer _cache;
        private CloudBlobContainer _drops;

        public AzureFileSystem(string account, string key) 
        {
            string cnxString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", account, key);
            var storageaccount = CloudStorageAccount.Parse(cnxString);
            var blobClient  = storageaccount.CreateCloudBlobClient();
            var _cache = blobClient.GetContainerReference("cache");
            var _drops = blobClient.GetContainerReference("drops");
        }

        public async Task<Manifest> GetManifest(string id)
        {
            string localpath = @"D:\scratch\dropscache\" + id;
            if (!File.Exists(localpath))
            {
                var manifestblob = _cache.GetBlockBlobReference(id + "/files.json");
                await manifestblob.DownloadToFileAsync(localpath, FileMode.CreateNew);
            }


            return new Manifest();
        }

        public override FileSystemEntry GetEntry(string path)
        {
            var uniqueid = path.Split('\\')[0];
            var manifest = GetManifest(uniqueid).Result;

            //find in manifest
            var entry = manifest.Find(path);
        	if (entry == null) return null;
        	// or throw new UnauthorizedAccessException("Given path is not allowed");

            return new FileSystemEntry(path, entry.Name, entry.isDirectory, 
            			entry.Length, 
            			/*Creation*/manifest.time, 
            			/*LastWriteTime*/manifest.time,
            			/*LastAccessTime*/manifest.time, 
            			/*isHidden*/false, /*isReadonly*/true, /*isArchived*/false);
           
        }

        public override FileSystemEntry CreateFile(string path)
        {
            throw new NotSupportedException();
        }

        public override FileSystemEntry CreateDirectory(string path)
        {
            throw new NotSupportedException();
        }

        public override void Move(string source, string destination)
        {
            throw new NotSupportedException();
        }

        public override void Delete(string path)
        {
            throw new NotSupportedException();
        }

        public override List<FileSystemEntry> ListEntriesInDirectory(string path)
        {
            var uniqueid = path.Split('\\')[0];
            var manifest = GetManifest(uniqueid).Result;
            var entries = manifest.Where(path);
        	
            return entries.Select(entry =>
            	new FileSystemEntry(path, entry.Name, entry.isDirectory, 
            			entry.Length, 
            			/*Creation*/manifest.time, 
            			/*LastWriteTime*/manifest.time,
            			/*LastAccessTime*/manifest.time, 
            			/*isHidden*/false, /*isReadonly*/true, /*isArchived*/false)).ToList();
        }

        public override Stream OpenFile(string path, FileMode mode, FileAccess access, FileShare share)
        {
            var uniqueid = path.Split('\\')[0];
            var manifest = GetManifest(uniqueid).Result;

            //find in manifest
            var entry = manifest.Find(path);
        	if (entry == null) return null;
            var cacheblob = _cache.GetBlockBlobReference(entry.Hash);
            return cacheblob.OpenRead(); //async? 
        }

        public override void SetAttributes(string path, bool? isHidden, bool? isReadonly, bool? isArchived)
        {
            throw new NotSupportedException();
        }

        public override void SetDates(string path, DateTime? creationDT, DateTime? lastWriteDT, DateTime? lastAccessDT)
        {
            throw new NotSupportedException();
        }

        public override string Name
        {
            get
            {
                return "AzureReadonlyCacheFS";
            }
        }

        public override long Size
        {
            get
            {
                return 0;
            }
        }

        public override long FreeSpace
        {
            get
            {
            	//could actually ask azure. Not sure we care.
                return long.Parse("5e15");
            }
        }
    }
}
