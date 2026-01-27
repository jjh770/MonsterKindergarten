using UnityEngine;
using UnityEngine.UI;

namespace Utility
{
    public static class SetImageScaleZero
    {
        public static void SetImageScaleToZero(this Image image)
        {
            image.transform.localScale = Vector3.zero;
            image.transform.localRotation = Quaternion.identity;
        }
    }
}
