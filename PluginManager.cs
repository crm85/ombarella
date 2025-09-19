using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ombarella
{
    public static class PluginManager
    {
        static bool _isRaidLastFrame = false;
        public static void Update()
        {
            bool isRaidThisFrame = Utils.IsInRaid();
            if (_isRaidLastFrame && !isRaidThisFrame)
            {
                Plugin.Instance.CleanupRaid();
            }
            else if (!_isRaidLastFrame && isRaidThisFrame)
            {
                Plugin.Instance.StartRaid();
            }
            _isRaidLastFrame = isRaidThisFrame;
        }
    }
}
