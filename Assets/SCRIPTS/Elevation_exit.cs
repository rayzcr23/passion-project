using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Elevation_exit : MonoBehaviour
{
    public Collider2D[] mountaincolliders;
    public Collider2D[] boundarycolliders;
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Player")
            foreach (Collider2D mountain in mountaincolliders)
            {
                mountain.enabled = true;
            }
        foreach (Collider2D boundary in boundarycolliders)
        {
            boundary.enabled = false;
        }
        collision.gameObject.GetComponent<SpriteRenderer>().sortingOrder = 1;
    }
}
