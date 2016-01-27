using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSP.IO;

namespace GravityTurn
{
    class LaunchDB
    {
        GravityTurner turner;

        public LaunchDB(GravityTurner inTurner)
        {
            turner = inTurner;
        }

        public string GetFilename()
        {
            return IOUtils.GetFilePathFor(turner.GetType(), string.Format("gt_launchdb_{0}_{1}.cfg", turner.LaunchName, turner.LaunchBody.name));
        }

        public void Load()
        {

        }
    }
}
