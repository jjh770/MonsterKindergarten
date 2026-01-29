using UnityEngine;
using UnityEngine.UI;

namespace Utility
{
    public static class SetScaleZero
    {
        public static void SetScaleToZero(this Image image)
        {
            image.transform.localScale = Vector3.zero;
            image.transform.localRotation = Quaternion.identity;
        }
    }
}
