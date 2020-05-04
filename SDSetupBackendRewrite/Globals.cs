﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using SDSetupBackendRewrite.Data;
using MongoDB.Driver;

namespace SDSetupBackendRewrite {
    public class Globals {
        public static string RootDirectory = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).DirectoryName;
    }
}
