using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ombarella
{
    public class Visualiser : MonoBehaviour
    {
        readonly string VIS_ZERO =      ">         <";
        readonly string VIS_ONE =       ">-        <";
        readonly string VIS_TWO =       ">--       <";
        readonly string VIS_THREE =     ">---      <";
        readonly string VIS_FOUR =      ">----     <";
        readonly string VIS_FIVE =      ">-----    <";
        readonly string VIS_SIX =       ">------   <";
        readonly string VIS_SEVEN =     ">-------  <";
        readonly string VIS_EIGHT =     ">-------- <";
        readonly string VIS_NINE =      ">---------<";

        //void OnGUI()
        //{
        //    float level = 1f;
        //    string display = GetLevelString(level);
        //}

        //string GetLevelString(float averageLight)
        //{
        //    string toDisplay;
        //    if (averageLight < 0.1f)
        //    {
        //        toDisplay = VIS_ZERO;
        //    }
        //    else if (averageLight < 0.2f)
        //    {
        //        toDisplay = VIS_ONE;
        //    }
        //}
    }
}
