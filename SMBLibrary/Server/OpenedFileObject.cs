/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace SMBLibrary.Server
{
    public class OpenedFileObject
    {
        public const int CacheSize = 1048576; // 1 MB

        public string Path;
        public bool IsSequentialAccess;
        public long CacheOffset;
        public byte[] Cache = new byte[0];

        public OpenedFileObject(string path, bool isSequentialAccess)
        {
            Path = path;
            IsSequentialAccess = isSequentialAccess;
        }
    }
}
