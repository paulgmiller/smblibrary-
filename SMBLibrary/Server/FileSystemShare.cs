/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;
using System.Linq;

namespace SMBLibrary.Server
{
    public class FileSystemShare
    {
        public string Name;
        public IEnumerable<string> ReadAccess;
        public IEnumerable<string> WriteAccess;
        public IFileSystem FileSystem;

        public bool HasReadAccess(string userName)
        {
            if (ReadAccess.First().Equals("*")) return true;

            return Contains(ReadAccess, userName);
        }

        public bool HasWriteAccess(string userName)
        {
            return Contains(WriteAccess, userName);
        }

        public static bool Contains(IEnumerable<string> list, string value)
        {
            return list.Any(item => item.Equals(value, StringComparison.InvariantCultureIgnoreCase));
        }

    }
}
