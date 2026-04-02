using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CMGAS
{
    public class CMName
    {
        private CMName() { }
        public CMName(string str)
        {
            CMStr = new CMString(str);
        }

        CMString CMStr;

        public override string ToString()
        {
            return CMStr.ToString();
        }
    }
}


