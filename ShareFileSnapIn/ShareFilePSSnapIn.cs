using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Management.Automation;
using ShareFile.Api.Powershell.Properties;
using System.Reflection;
using System.Collections;

namespace ShareFile.Api.Powershell
{
    [RunInstaller(true)]
    public class ShareFilePSSnapIn : PSSnapIn
    {
       
        /// <summary>
        /// Create an instance of the GetProcPSSnapIn01 class.
        /// </summary>
        public ShareFilePSSnapIn()
            : base()
        {
        }

        /// <summary>
        /// Specify the name of the PowerShell snap-in.
        /// </summary>
        public override string Name
        {
            get
            {
                return "ShareFile";
            }
        }

        /// <summary>
        /// Specify the vendor for the PowerShell snap-in.
        /// </summary>
        public override string Vendor
        {
            get
            {
                return "Citrix";
            }
        }

        /// <summary>
        /// Specify a description of the PowerShell snap-in.
        /// </summary>
        public override string Description
        {
            get
            {
                return "PowerShell Snap-In for ShareFile API. Version " + Resources.Version;
            }
        }

        /// <summary>The format file for the snap-in. </summary> 
        private string[] _formats = { "ShareFile.Format.ps1xml" }; 
        public override string[] Formats { get { return _formats ; } }

        public static Assembly MyResolveEventHandler(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("System.Net.Http.Primitives"))
            {
                var assembly = System.Reflection.Assembly.LoadFrom("System.Net.Http.Primitives.dll");
                return assembly;
            }
            return null;
        }
    }
}
