namespace ombarella
{
    public static class Visualiser
    {
        static readonly string VIS_ZERO =      "}           {";
        static readonly string VIS_ONE =       "} ]         {";
        static readonly string VIS_TWO =       "} ]]        {";
        static readonly string VIS_THREE =     "} ]]]       {";
        static readonly string VIS_FOUR =      "} ]]]]      {";
        static readonly string VIS_FIVE =      "} ]]]]]     {";
        static readonly string VIS_SIX =       "} ]]]]]]    {";
        static readonly string VIS_SEVEN =     "} ]]]]]]]   {";
        static readonly string VIS_EIGHT =     "} ]]]]]]]]  {";
        static readonly string VIS_NINE =      "} ]]]]]]]]] {";


        public static string GetLevelString(float averageLight, bool isDebug)
        {
            if (isDebug)
            {
                string debugString = string.Format($"debug value = {averageLight}");
                return debugString;
            }
            string toDisplay;
            if (averageLight < 0.1f)
            {
                toDisplay = VIS_ZERO;
            }
            else if (averageLight < 0.2f)
            {
                toDisplay = VIS_ONE;
            }
            else if (averageLight < 0.3f)
            {
                toDisplay = VIS_TWO;
            }
            else if (averageLight < 0.4f)
            {
                toDisplay = VIS_THREE;
            }
            else if (averageLight < 0.5f)
            {
                toDisplay = VIS_FOUR;
            }
            else if (averageLight < 0.6f)
            {
                toDisplay = VIS_FIVE;
            }
            else if (averageLight < 0.7f)
            {
                toDisplay = VIS_SIX;
            }
            else if (averageLight < 0.8f)
            {
                toDisplay = VIS_SEVEN;
            }
            else if (averageLight < 0.9f)
            {
                toDisplay = VIS_EIGHT;
            }
            else
            {
                toDisplay = VIS_NINE;
            }
            return toDisplay;
        }
    }
}
