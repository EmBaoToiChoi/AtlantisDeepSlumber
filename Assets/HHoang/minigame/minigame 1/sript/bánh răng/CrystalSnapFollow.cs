using UnityEngine;
using Unity.Netcode;

public class CrystalSnapFollow : NetworkBehaviour
{
    private CrystalCore core;
    [HideInInspector] public Transform targetSnapPoint; 

    void Awake() 
    {
        core = GetComponent<CrystalCore>();
    }

    // ĐÃ XÓA HẲN TÍNH NĂNG FOLLOW TRONG FIXEDUPDATE THEO YÊU CẦU[cite: 2]
}