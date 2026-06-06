using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class AscensionManager : NetworkBehaviour
{
    [Header("Cấu hình Particle Dòng Chảy")]
    public ParticleSystem[] flowParticles; 
    
    [Header("Cấu hình Trụ")]
    public Transform[] pillarPositions = new Transform[4];
    public int[] pillarStates = new int[4]; 

    [Header("Cấu hình Hiệu ứng Chiến thắng")]
    public GameObject victoryEffectObject; 

    private List<CrystalCore> placedCrystals = new List<CrystalCore>();
    private Coroutine timerCoroutine;
    private bool isTimerRunning = false;
    
    [Header("Cấu hình Thời gian")]
    public float timeLimit = 5f; 

    void Start()
    {
        foreach (var ps in flowParticles) if (ps != null) ps.gameObject.SetActive(false);
        if (victoryEffectObject != null) victoryEffectObject.SetActive(false);
    }

    void Update()
    {
        placedCrystals.RemoveAll(item => item == null);
    }

    public void SnapCrystalToPillar(CrystalCore crystal, int stationIndex)
    {
        if (!IsServer || stationIndex < 0 || stationIndex >= pillarPositions.Length) return;

        crystal.LockToStation(); 

        var snapFollow = crystal.GetComponent<CrystalSnapFollow>();
        if (snapFollow != null)
        {
            snapFollow.targetSnapPoint = pillarPositions[stationIndex].GetComponent<PillarStation>().snapPosition;
        }

        pillarStates[stationIndex] = crystal.crystalID; 
        if (!placedCrystals.Contains(crystal)) placedCrystals.Add(crystal);

        if (!isTimerRunning && placedCrystals.Count == 1)
        {
            isTimerRunning = true;
            timerCoroutine = StartCoroutine(TimerCountdown());
        }
        
        CheckWinCondition();
    }

    [ServerRpc(RequireOwnership = false)]
    public void UpdateSnappedStateServerRpc(NetworkObjectReference crystalRef, bool state)
    {
        if (crystalRef.TryGet(out NetworkObject netObj))
        {
            var crystal = netObj.GetComponent<CrystalCore>();
            if (crystal != null) crystal.isSnapped.Value = state;
        }
    }

    IEnumerator TimerCountdown()
    {
        float timeLeft = timeLimit;
        while (timeLeft > 0)
        {
            yield return new WaitForSeconds(1f);
            timeLeft--;
        }

        if (placedCrystals.Count < 4) EjectAllCrystals();
        isTimerRunning = false;
    }

    void EjectAllCrystals()
    {
        if (!IsServer) return;

        foreach (var crystal in placedCrystals)
        {
            if (crystal != null)
            {
                // Bật lại va chạm bình thường, không cần chặn reset nữa
                var col = crystal.GetComponent<Collider>();
                if (col != null) col.enabled = true; 

                UpdateSnappedStateServerRpc(crystal.NetworkObject, false);

                Rigidbody rb = crystal.GetComponent<Rigidbody>();
                if (rb != null) 
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    // Văng ra theo lực nhẹ thôi để nó không văng quá xa
                    rb.AddForce(new Vector3(Random.Range(-2f, 2f), 5f, Random.Range(-2f, 2f)), ForceMode.Impulse);
                }
            }
        }

        foreach (var pillar in pillarPositions)
        {
            if (pillar != null)
            {
                PillarStation station = pillar.GetComponent<PillarStation>();
                if (station != null) station.isOccupied.Value = false; 
            }
        }

        foreach (var ps in flowParticles) if (ps != null) { ps.Stop(); ps.gameObject.SetActive(false); }

        placedCrystals.Clear();
        for (int i = 0; i < pillarStates.Length; i++) pillarStates[i] = 0;
        
        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
        isTimerRunning = false;
    }

    // Coroutine hỗ trợ bật lại va chạm
    IEnumerator ReenableCollider(Collider col)
    {
        yield return new WaitForSeconds(0.5f);
        if (col != null) col.enabled = true;
    }

    void CheckWinCondition()
    {
        if (placedCrystals.Count < 4) return;

        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
        isTimerRunning = false;

        bool allCorrect = true;
        for (int i = 0; i < pillarPositions.Length; i++)
        {
            bool isCorrect = (pillarStates[i] == i);
            SetFlowColorClientRpc(i, isCorrect ? Color.green : Color.red);
            if (!isCorrect) allCorrect = false;
        }

        if (allCorrect) TriggerVictoryEffectsClientRpc();
        else StartCoroutine(DelayEject());
    }

    [ClientRpc]
    private void TriggerVictoryEffectsClientRpc()
    {
        if (victoryEffectObject != null) victoryEffectObject.SetActive(true);
    }

    [ClientRpc]
    private void SetFlowColorClientRpc(int stationIndex, Color color)
    {
        if (stationIndex < 0 || stationIndex >= flowParticles.Length) return;
        ParticleSystem ps = flowParticles[stationIndex];
        if (ps != null)
        {
            ps.gameObject.SetActive(true);
            var main = ps.main;
            
            // Code tự nhuộm màu hạt trực tiếp mà không cần đổi Material
            main.startColor = color; 
            
            if (!ps.isPlaying) ps.Play();
        }
    }

    IEnumerator DelayEject()
    {
        yield return new WaitForSeconds(8.0f); 
        EjectAllCrystals();
    }
}