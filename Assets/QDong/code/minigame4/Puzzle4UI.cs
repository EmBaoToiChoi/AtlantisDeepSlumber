using TMPro;
using UnityEngine;
using Unity.Netcode;

public class Puzzle4UI : NetworkBehaviour
{
    public EnergyColumn A;
    public EnergyColumn B;
    public EnergyColumn C;
    public EnergyColumn D;

    public TMP_Text aText;
    public TMP_Text bText;
    public TMP_Text cText;
    public TMP_Text dText;

    void Update()
    {
        if (A == null ||
            B == null ||
            C == null ||
            D == null)
            return;

        aText.text =
            "A : " +
            Mathf.RoundToInt(A.charge.Value) +
            "%";

        bText.text =
            "B : " +
            Mathf.RoundToInt(B.charge.Value) +
            "%";

        cText.text =
            "C : " +
            Mathf.RoundToInt(C.charge.Value) +
            "%";

        dText.text =
            "C : " +
            Mathf.RoundToInt(C.charge.Value) +
            "%";

        dText.text =
            "D : " +
            Mathf.RoundToInt(D.charge.Value) +
            "%";
    }
}