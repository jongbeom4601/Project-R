using UnityEngine;

namespace ProjectR
{
    public sealed class SpatialManager : MonoBehaviour
    {
        [SerializeField] private GameObject normalCorridor;
        [SerializeField] private GameObject extendedCorridor;
        [SerializeField] private GameObject[] normalEndWalls;

        public bool IsExtended => extendedCorridor.activeSelf;

        public void ExtendCorridor()
        {
            if (IsExtended)
                return;

            if (normalEndWalls == null || normalEndWalls.Length == 0)
                normalCorridor.SetActive(false);
            else
                foreach (GameObject wall in normalEndWalls)
                    wall.SetActive(false);
            extendedCorridor.SetActive(true);
        }
    }
}
