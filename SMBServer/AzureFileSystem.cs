/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
       //worst filesystem ever
      

        public Manifest(string json, Func<string, ulong> getSize ) : this(getSize) 
        {
            var _hashDict = JsonConvert.DeserializeObject<Dictionary<string,List<string>>>(json);   
            foreach (var kvp in _hashDict)
            {
                foreach (var link in kvp.Value)
                {
                    Add(link.Split('\\'), kvp.Key);
                }
            }
        }

        private Manifest(Func<string, ulong> getSize) { GetSize = getSize;  }

        Dictionary<string, Manifest> folders = new Dictionary<string, Manifest>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Func<string, ulong> GetSize;
        private void Add(IEnumerable<string> path, string hash)
        {
            if (!path.Any()) throw new ArgumentException("no path");
            var name = path.First();
            if (path.Count() == 1) 
            {
                if (!files.ContainsKey(name))
                {
                    files.Add(name, hash);
                }
                return;
            } 
            if (!folders.ContainsKey(name))
            {
                folders.Add(name, new Manifest(GetSize));
            }  
            folders[name].Add(path.Skip(1), hash);               
        }
        

        public DateTime time { get { return DateTime.Now; } }
        public FileSystemEntry GetEntry(IEnumerable<string> path, string fullpath )  
        {
          if (!path.Any()) throw new ArgumentException("no path");
          string name = path.First();
          if (path.Count() == 1)
          {
              string hash;
              if (files.TryGetValue(name, out hash))
              {
                  return CreateFile(fullpath, name, hash);
              }
              if (folders.ContainsKey(name))
              {
                  return CreateDirectory(fullpath, name);
              }
              return null;
          }
          Manifest m;

          if (folders.TryGetValue(name, out m))
          {
              return m.GetEntry(path.Skip(1), fullpath);
          }
          return null;
                
        }

        public List<FileSystemEntry> ListEntriesInDirectory(IEnumerable<string> path, string fullpath)
        {
          if (!path.Any()) 
          {
             var folderentries = folders.Keys.Select(folder =>
                   CreateDirectory(Path.Combine(fullpath,folder),folder));
             var fileentries = files.Select(file => 
                    CreateFile(Path.Combine(fullpath,file.Key), file.Key, file.Value));
             return folderentries.Concat(fileentries).ToList();
          }
          string name = path.First();
          Manifest m;
          if (folders.TryGetValue(name, out m))
          {
              return m.ListEntriesInDirectory(path.Skip(1), fullpath);
          }
          return null; 
        }

        private FileSystemEntry CreateDirectory(string path, string name)
        {
            return new FileSystemEntry(path, name, /*isDirectory*/true, 0,
                /*Creation*/time, /*LastWriteTime*/time, /*LastAccessTime*/time,
                /*isHidden*/false, /*isReadonly*/true, /*isArchived*/false);
        }

        private FileSystemEntry CreateFile(string path, string name, string hash)
        {
            return new FileSystemEntry(path, name, false, (ulong)GetSize(hash),
                /*Creation*/time, /*LastWriteTime*/time, /*LastAccessTime*/time,
                /*isHidden*/false, /*isReadonly*/true, /*isArchived*/false);
        }

        public string GetHash(IEnumerable<string> path)
        {
          if (!path.Any()) throw new ArgumentException("have to have some path to file");

          string name = path.First();
          
          if (path.Count() == 1)
          {
             string hash;
             if (files.TryGetValue(name, out hash))
                 return hash;
              return null;
          }
          Manifest m;
          if (folders.TryGetValue(name, out m))
          {
              return m.GetHash(path.Skip(1));
          }
          return null; //empty?
        }

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
            _cache = blobClient.GetContainerReference("cache");
            _drops = blobClient.GetContainerReference("drops");
        }

        private ConcurrentDictionary<string, Manifest> manifests = new ConcurrentDictionary<string, Manifest>(StringComparer.OrdinalIgnoreCase);

        public Manifest GetManifest(string id)
        {
            id = id.ToLower();
            return manifests.GetOrAdd(id, newid =>
            {
                var manifestblob = _drops.GetBlockBlobReference(newid + "/files.json");
                //var stream = new MemoryStream();
                //A stream would be more effective here.
                return new Manifest(manifestblob.DownloadText(), this.GetFileSize);
            });
        }

        public override FileSystemEntry GetEntry(string path)
        {
            var parts = path.Split(new[] {"\\"}, StringSplitOptions.RemoveEmptyEntries);
            if (!parts.Any())
            {
                var time = DateTime.Now;
                return new FileSystemEntry(path, "drops", true, 0,
                    /*Creation*/time, /*LastWriteTime*/time, /*LastAccessTime*/time,
                    /*isHidden*/false, /*isReadonly*/true, /*isArchived*/false);
            }
            var manifest = GetManifest(parts.First());
            if (manifest == null) return null;
            //find in manifest
            if (parts.Count() == 1)
            {
                var time = DateTime.Now;
                return new FileSystemEntry(path, parts.First(), true, 0,
                    /*Creation*/time, /*LastWriteTime*/time, /*LastAccessTime*/time,
                    /*isHidden*/false, /*isReadonly*/true, /*isArchived*/false);
            }
            
            return manifest.GetEntry(parts.Skip(1), path);
           
        }

        private ConcurrentDictionary<string, ulong> sizes = new ConcurrentDictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);

       

        public ulong GetFileSize(string hash)
        {
            return sizes.GetOrAdd(hash, lookuphash =>
            {
                var cacheblob = _cache.GetBlockBlobReference(lookuphash);
                cacheblob.FetchAttributes();
                return (ulong)cacheblob.Properties.Length;
            });
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
            //throw if parts is empty?
            var parts = path.Split(new[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
            var manifest = GetManifest(parts.First());
            if (manifest == null) return new FileSystemEntry[] { }.ToList(); //null?
            //find in manifest
            return manifest.ListEntriesInDirectory(parts.Skip(1), path);
        }

        public override Stream OpenFile(string path, FileMode mode, FileAccess access, FileShare share)
        {
            //throw on mode other than read. Ignore everything else?
            //throw if parts is empty?
            var parts = path.Split(new[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
            var manifest = GetManifest(parts.First());
            if (manifest == null) return null;
            //find in manifest
            var hash = manifest.GetHash(parts.Skip(1));

        	if (hash == null) return null;
            var cacheblob = _cache.GetBlockBlobReference(hash);
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
