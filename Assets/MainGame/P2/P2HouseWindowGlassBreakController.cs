using UnityEngine;

namespace MainGame.P2
{
    public sealed class P2HouseWindowGlassBreakController : MonoBehaviour
    {
        public void BreakAllHouseGlass()
        {
            P2BreakableWindowGlass.BreakAllHouseGlass();
        }
    }
}
