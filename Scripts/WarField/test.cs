using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class test : MonoBehaviour
{
    void OnBecameVisible() => Debug.Log($"{name} became VISIBLE");
    void OnBecameInvisible() => Debug.Log($"{name} became INVISIBLE");
}
