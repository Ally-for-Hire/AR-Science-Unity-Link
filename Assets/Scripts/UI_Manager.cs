using UnityEngine;
using System.Collections;

public class UI_Manager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject splashScreen;
    [SerializeField] private GameObject nextScreen;

    [Header("Settings")]
    [SerializeField] private float delayInSeconds = 2.0f;

    private Coroutine splashCoroutine;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Always start at splash screen, even if the next screen is active in the editor
        if (!splashScreen.activeSelf) splashScreen.SetActive(true);
        if (nextScreen.activeSelf) nextScreen.SetActive(false);

        if (splashCoroutine != null)
        {
            StopCoroutine(splashCoroutine);
            splashCoroutine = null;
        }

        splashCoroutine = StartCoroutine(SplashCoroutine(splashScreen, nextScreen, delayInSeconds));
    }

    private IEnumerator SplashCoroutine(GameObject from, GameObject to, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (to != null) to.SetActive(true);
        if (from != null) from.SetActive(false);

        splashCoroutine = null;
    }
}
