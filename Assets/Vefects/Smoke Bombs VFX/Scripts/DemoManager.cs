using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DemoManager : MonoBehaviour
{
    public ParticleSystem[] VFXList;
    int currentVFXIndex;
    GameObject spawnedVFX;

    public Transform spawnOffSet;

    float loopTime = 6f;
    float currentTime;
 

    void Start()
    {
        //currentTime = loopTime;
    }

    void Update()
    {
        currentTime -= Time.deltaTime;

        if (currentTime <= 0)
        {
            SpawnVFX();
        }

        InputsFXElement();


        void InputsFXElement()
        {
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (currentVFXIndex < VFXList.Length - 1)
                {
                    currentVFXIndex += 1;
                }

                else if (currentVFXIndex >= VFXList.Length - 1)
                {
                    currentVFXIndex = 0;
                }

                SpawnVFX();
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (currentVFXIndex > 0)
                {
                    currentVFXIndex -= 1;
                }

                else if (currentVFXIndex <= 0)
                {
                    currentVFXIndex = VFXList.Length - 1;
                }

                SpawnVFX();
            }
        }

        void SpawnVFX()
        {
            Destroy(spawnedVFX);
            spawnedVFX = Instantiate(VFXList[currentVFXIndex].gameObject, spawnOffSet.position, spawnOffSet.rotation).gameObject;
            currentTime = loopTime;

        }
    }
}
