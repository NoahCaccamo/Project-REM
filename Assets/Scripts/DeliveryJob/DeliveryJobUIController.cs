using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DeliveryJobUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject jobListPanel;
    [SerializeField] private Transform jobListContent;
    [SerializeField] private GameObject jobEntryPrefab;
    [SerializeField] private Button openJobListButton;
    [SerializeField] private Button closeJobListButton;

    [Header("Active Job Display")]
    [SerializeField] private GameObject activeJobPanel;
    [SerializeField] private TextMeshProUGUI activeJobText;

    private List<GameObject> jobEntries = new();

    private void Start()
    {
        if (openJobListButton != null)
            openJobListButton.onClick.AddListener(OpenJobList);

        if (closeJobListButton != null)
            closeJobListButton.onClick.AddListener(CloseJobList);

        jobListPanel.SetActive(false);
        UpdateActiveJobDisplay();
    }

    private void Update()
    {
        // Update active job display continuously
        if (DeliveryJobManager.Instance != null)
        {
            UpdateActiveJobDisplay();
        }

        if (Input.GetKeyDown(KeyCode.J))
        {
            if (jobListPanel.activeSelf)
            {
                CloseJobList();
            }
            else
            {
                OpenJobList();
            }
        }
    }

    public void OpenJobList()
    {
        jobListPanel.SetActive(true);
        RefreshJobList();

        // Simulate cursor visibility (you may want to implement proper cursor management)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseJobList()
    {
        jobListPanel.SetActive(false);

        // Return to gameplay cursor state
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void RefreshJobList()
    {
        // Clear existing entries
        foreach (var entry in jobEntries)
        {
            Destroy(entry);
        }
        jobEntries.Clear();

        if (DeliveryJobManager.Instance == null) return;

        var jobs = DeliveryJobManager.Instance.GetAvailableJobs();

        foreach (var job in jobs)
        {
            GameObject entry = Instantiate(jobEntryPrefab, jobListContent);
            jobEntries.Add(entry);

            // Setup the entry (assumes specific child structure in prefab)
            var jobEntry = entry.GetComponent<DeliveryJobEntry>();
            if (jobEntry != null)
            {
                jobEntry.Setup(job, this);
            }
        }
    }

    public void SelectJob(DeliveryJob job)
    {
        if (DeliveryJobManager.Instance != null)
        {
            bool success = DeliveryJobManager.Instance.SetActiveJob(job);
            if (success)
            {
                CloseJobList();
                UpdateActiveJobDisplay();
            }
        }
    }

    private void UpdateActiveJobDisplay()
    {
        if (DeliveryJobManager.Instance == null)
        {
            activeJobPanel.SetActive(false);
            return;
        }

        var activeJob = DeliveryJobManager.Instance.GetActiveJob();

        if (activeJob != null)
        {
            activeJobPanel.SetActive(true);
            activeJobText.text = $"Active: {activeJob.memoryType.themeName}";
        }
        else
        {
            activeJobPanel.SetActive(false);
        }
    }
}