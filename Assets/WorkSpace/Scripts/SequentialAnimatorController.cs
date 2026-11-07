using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SequentialAnimatorController : MonoBehaviour
{
    [Serializable]
    public class ObjectSequence
    {
        public string name = "New Recipe";
        [Tooltip("Is poori recipe ka root parent GameObject (e.g. 1st Recipe, 2nd Recipe)")]
        public GameObject recipeRootObject;

        [Tooltip("Is recipe ke items (Flour, Eggs, Sugar etc.). Dynamic count.")]
        public List<GameObject> objects = new List<GameObject>();

        [Header("Recipe Specific Transition Timing")]
        [Tooltip("Is specific recipe ke complete hone par loading screen aane ka delay")]
        [Min(0f)] public float delayBeforeLoadingPanel = 1.2f;

        [Header("Mixer / Tool Selection")]
        [Tooltip("Tick karein agar items complete hone par mixer panel khulwana ho.")]
        public bool requireToolSelection = false;
        public GameObject toolSelectionPanel;

        [Header("Mixer Objects (Scene References)")]
        public GameObject handMixerObject;
        public GameObject machineMixerObject;

        [Header("Legacy Completion Panel (Optional)")]
        public bool showPanelWhenComplete;
        public GameObject completionPanel;
        [Min(0f)] public float panelDuration = 2f;
    }

    public static SequentialAnimatorController Instance { get; private set; }

    [Header("Sequence Configurations")]
    [SerializeField] private List<ObjectSequence> sequences = new List<ObjectSequence>();
    [SerializeField, Min(0f)] private float previousObjectDisableDelay = 3f;

    [Header("Mixing Progress Bar (Central UI)")]
    [Tooltip("Bottom par aane wali filling slider bar")]
    [SerializeField] private Slider mixingProgressBar;

    [Header("Transition Loading Panel")]
    [Tooltip("Recipe change hone ke darmiyan jo loading screen aayegi")]
    [SerializeField] private GameObject recipeLoadingPanel;
    [SerializeField, Min(0f)] private float recipeLoadingDuration = 1.2f;
    [Tooltip("Fallback global delay agar kisi recipe ka apna delay 0 ho")]
    [SerializeField, Min(0f)] private float delayBeforeLoadingPanel = 1.2f;

    [Header("Item Complete Particles")]
    [SerializeField] private ParticleSystem[] completionParticles;
    [SerializeField, Min(0f)] private float completionParticleDelay = 0.25f;

    [SerializeField, HideInInspector] private List<GameObject> objects1 = new List<GameObject>();
    [SerializeField, HideInInspector] private List<GameObject> objects2 = new List<GameObject>();

    private int currentObjectIndex;
    private int currentSequenceIndex;
    private bool isChangingSequence;
    private bool isChangingItem;
    private bool isToolSelectionPhase;
    private GameObject currentActiveTool;

    public IReadOnlyList<ObjectSequence> Sequences => sequences;

    private void Awake()
    {
        MigrateLegacyLists();
        Instance = this;
        Initialize();
    }

    private void OnValidate()
    {
        MigrateLegacyLists();
    }

    private void Start()
    {
        if (recipeLoadingPanel != null)
            recipeLoadingPanel.SetActive(false);

        HideProgressBar();
        PlayCurrentTask();
    }

    /// <summary>Enter a registered stage directly, including a presentation with no ingredient tasks.</summary>
    public void StartSequence(GameObject recipeRoot)
    {
        int index = sequences.FindIndex(sequence => sequence.recipeRootObject == recipeRoot);
        if (index < 0) { Debug.LogError("Presentation stage is not registered in the sequence.", recipeRoot); return; }
        StopAllCoroutines();
        currentSequenceIndex = index;
        currentObjectIndex = 0;
        isChangingSequence = isChangingItem = isToolSelectionPhase = false;
        currentActiveTool = null;
        HideProgressBar();
        if (recipeLoadingPanel != null) recipeLoadingPanel.SetActive(false);
        foreach (ObjectSequence sequence in sequences)
            if (sequence.recipeRootObject != null && sequence.recipeRootObject != recipeRoot)
                sequence.recipeRootObject.SetActive(false);
        recipeRoot.SetActive(true);
        if (sequences[index].objects.Count > 0)
        {
            ActivateAllObjectsInCurrentSequence(sequences[index]);
            PlayCurrentTask();
        }
    }

    public void Initialize()
    {
        StopAllCoroutines();
        currentSequenceIndex = 0;
        currentObjectIndex = 0;
        isChangingSequence = false;
        isChangingItem = false;
        isToolSelectionPhase = false;
        currentActiveTool = null;

        HideProgressBar();

        for (int i = 0; i < sequences.Count; i++)
        {
            ObjectSequence seq = sequences[i];
            SetSequenceActive(seq, false);

            if (seq.recipeRootObject != null)
                seq.recipeRootObject.SetActive(i == 0);

            if (seq.toolSelectionPanel != null)
                seq.toolSelectionPanel.SetActive(false);

            if (seq.handMixerObject != null)
                seq.handMixerObject.SetActive(false);

            if (seq.machineMixerObject != null)
                seq.machineMixerObject.SetActive(false);
        }

        if (sequences.Count > 0)
            ActivateAllObjectsInCurrentSequence(sequences[0]);

        SetCompletionPanelsActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void PlayCurrentTask()
    {
        while (currentSequenceIndex < sequences.Count)
        {
            ObjectSequence sequence = sequences[currentSequenceIndex];

            if (sequence == null || sequence.objects == null ||
                currentObjectIndex >= sequence.objects.Count)
            {
                HandleSequenceItemsCompleted();
                return;
            }

            UpdateObjectsInteractiveState(sequence);
            return;
        }
    }

    private void ActivateAllObjectsInCurrentSequence(ObjectSequence sequence)
    {
        if (sequence == null || sequence.objects == null) return;

        foreach (GameObject obj in sequence.objects)
        {
            if (obj != null)
                obj.SetActive(true);
        }

        UpdateObjectsInteractiveState(sequence);
    }

    private void UpdateObjectsInteractiveState(ObjectSequence sequence)
    {
        if (sequence == null || sequence.objects == null) return;

        for (int i = 0; i < sequence.objects.Count; i++)
        {
            GameObject obj = sequence.objects[i];
            if (obj == null) continue;

            if (i < currentObjectIndex)
                continue;

            bool isCurrentTurn = (i == currentObjectIndex);

            Collider2D[] colliders = obj.GetComponentsInChildren<Collider2D>(true);
            foreach (Collider2D col in colliders)
                col.enabled = isCurrentTurn;

            Animator rootAnim = obj.GetComponent<Animator>();
            if (rootAnim != null)
            {
                rootAnim.enabled = isCurrentTurn;
                Animator[] childAnimators = obj.GetComponentsInChildren<Animator>(true);
                foreach (Animator anim in childAnimators)
                {
                    if (anim != rootAnim) anim.enabled = false;
                }
            }
            else
            {
                Animator[] childAnimators = obj.GetComponentsInChildren<Animator>(true);
                foreach (Animator anim in childAnimators)
                {
                    anim.enabled = isCurrentTurn;
                }
            }
        }
    }

    public void UpdateTask()
    {
        if (isChangingSequence || isChangingItem || currentSequenceIndex >= sequences.Count)
            return;

        if (isToolSelectionPhase)
        {
            HideProgressBar();

            if (currentActiveTool != null)
            {
                StartCoroutine(DisableObjectAfterDelay(currentActiveTool,true));
                currentActiveTool = null;
            }

            isToolSelectionPhase = false;
            StartCoroutine(CompleteCurrentSequence());
            return;
        }

        ObjectSequence currentSequence = sequences[currentSequenceIndex];
        if (currentSequence != null && currentSequence.objects != null &&
            currentObjectIndex >= 0 && currentObjectIndex < currentSequence.objects.Count)
        {
            GameObject completedObject = currentSequence.objects[currentObjectIndex];
            if (completedObject != null)
                StartCoroutine(DisableObjectAfterDelay(completedObject,false));
        }

        StartCoroutine(CompleteCurrentItem());
    }

    private IEnumerator CompleteCurrentItem()
    {
        isChangingItem = true;

        if (completionParticleDelay > 0f)
            yield return new WaitForSeconds(completionParticleDelay);

        PlayRandomCompletionParticle();

        currentObjectIndex++;
        isChangingItem = false;

        PlayCurrentTask();
    }

    private IEnumerator DisableObjectAfterDelay(GameObject completedObject, bool flag)
    {
        if (previousObjectDisableDelay > 0f)
            yield return new WaitForSeconds(previousObjectDisableDelay);

        if (completedObject != null && flag)
            completedObject.SetActive(false);
    }

    private void HandleSequenceItemsCompleted()
    {
        ObjectSequence currentSequence = sequences[currentSequenceIndex];

        if (currentSequence != null && currentSequence.requireToolSelection)
        {
            isToolSelectionPhase = true;

            if (currentSequence.toolSelectionPanel != null)
                currentSequence.toolSelectionPanel.SetActive(true);
        }
        else
        {
            StartCoroutine(CompleteCurrentSequence());
        }
    }

    public void ShowProgressBar(float initialValue = 0f)
    {
        if (mixingProgressBar != null)
        {
            mixingProgressBar.value = initialValue;
            mixingProgressBar.gameObject.SetActive(true);
        }
    }

    public void UpdateProgressBar(float normalizedProgress)
    {
        if (mixingProgressBar != null && mixingProgressBar.gameObject.activeSelf)
        {
            mixingProgressBar.value = Mathf.Clamp01(normalizedProgress);
        }
    }

    public void HideProgressBar()
    {
        if (mixingProgressBar != null)
        {
            mixingProgressBar.gameObject.SetActive(false);
        }
    }

    public void OnSelectHandMixer()
    {
        if (currentSequenceIndex >= sequences.Count) return;
        ObjectSequence currentSequence = sequences[currentSequenceIndex];

        if (currentSequence.toolSelectionPanel != null)
            currentSequence.toolSelectionPanel.SetActive(false);

        if (currentSequence.machineMixerObject != null)
            currentSequence.machineMixerObject.SetActive(false);

        if (currentSequence.handMixerObject != null)
        {
            currentSequence.handMixerObject.SetActive(true);
            currentActiveTool = currentSequence.handMixerObject;
            EnableToolInteraction(currentActiveTool);
        }
    }

    public void OnSelectMachineMixer()
    {
        if (currentSequenceIndex >= sequences.Count) return;
        ObjectSequence currentSequence = sequences[currentSequenceIndex];

        if (currentSequence.toolSelectionPanel != null)
            currentSequence.toolSelectionPanel.SetActive(false);

        if (currentSequence.handMixerObject != null)
            currentSequence.handMixerObject.SetActive(false);

        if (currentSequence.machineMixerObject != null)
        {
            currentSequence.machineMixerObject.SetActive(true);
            currentActiveTool = currentSequence.machineMixerObject;
            EnableToolInteraction(currentActiveTool);
        }
    }

    private void EnableToolInteraction(GameObject toolObj)
    {
        if (toolObj == null) return;

        Collider2D[] colliders = toolObj.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D col in colliders)
            col.enabled = true;

        Animator parentAnim = toolObj.GetComponent<Animator>();
        if (parentAnim != null)
            parentAnim.enabled = true;

        SpriteHoldAndMove holdScript = toolObj.GetComponentInChildren<SpriteHoldAndMove>(true);
        if (holdScript != null && holdScript.mixerToolAnimator != null)
        {
            holdScript.mixerToolAnimator.enabled = false;
        }
    }

    private IEnumerator CompleteCurrentSequence()
    {
        isChangingSequence = true;

        PlayRandomCompletionParticle();

        // 1. Current Recipe ka delay check karein
        ObjectSequence completedSequence = (currentSequenceIndex >= 0 && currentSequenceIndex < sequences.Count)
            ? sequences[currentSequenceIndex]
            : null;

        float activeDelay = (completedSequence != null) ? completedSequence.delayBeforeLoadingPanel : delayBeforeLoadingPanel;

        if (activeDelay > 0f)
            yield return new WaitForSeconds(activeDelay);

        if (completedSequence != null && completedSequence.showPanelWhenComplete &&
            completedSequence.completionPanel != null)
        {
            completedSequence.completionPanel.SetActive(true);
            if (completedSequence.panelDuration > 0f)
                yield return new WaitForSeconds(completedSequence.panelDuration);
            completedSequence.completionPanel.SetActive(false);
        }

        bool hasNextRecipe = (currentSequenceIndex + 1) < sequences.Count;

        if (hasNextRecipe)
        {
            if (recipeLoadingPanel != null)
                recipeLoadingPanel.SetActive(true);

            if (completedSequence != null && completedSequence.recipeRootObject != null)
                completedSequence.recipeRootObject.SetActive(false);

            currentSequenceIndex++;
            currentObjectIndex = 0;
            isToolSelectionPhase = false;

            ObjectSequence nextSequence = sequences[currentSequenceIndex];

            if (nextSequence.recipeRootObject != null)
                nextSequence.recipeRootObject.SetActive(true);

            ActivateAllObjectsInCurrentSequence(nextSequence);

            if (recipeLoadingDuration > 0f)
                yield return new WaitForSeconds(recipeLoadingDuration);

            if (recipeLoadingPanel != null)
                recipeLoadingPanel.SetActive(false);
        }
        else
        {
            currentSequenceIndex++;
            currentObjectIndex = 0;
            isToolSelectionPhase = false;
            Debug.Log("Entire Game Finished!");
        }

        isChangingSequence = false;
        PlayCurrentTask();
    }

    private void PlayRandomCompletionParticle()
    {
        // Particles ke sath success chime play karein
        if (SoundManager.Instance != null && SoundManager.Instance.successSparkleSound != null)
        {
            SoundManager.Instance.PlaySound(SoundManager.Instance.successSparkleSound);
        }

        if (completionParticles == null || completionParticles.Length == 0)
            return;

        int validCount = 0;
        foreach (var p in completionParticles)
            if (p != null) validCount++;

        if (validCount == 0) return;

        int randomIndex = UnityEngine.Random.Range(0, validCount);

        foreach (var particle in completionParticles)
        {
            if (particle == null) continue;
            if (randomIndex-- > 0) continue;

            if (!particle.gameObject.activeSelf)
                particle.gameObject.SetActive(true);

            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play(true);
            return;
        }
    }

    private void SetCompletionPanelsActive(bool isActive)
    {
        foreach (ObjectSequence sequence in sequences)
        {
            if (sequence != null && sequence.completionPanel != null)
                sequence.completionPanel.SetActive(isActive);
        }
    }

    private static void SetSequenceActive(ObjectSequence sequence, bool isActive)
    {
        if (sequence == null || sequence.objects == null) return;
        foreach (GameObject obj in sequence.objects)
            if (obj != null) obj.SetActive(isActive);
    }

    private void MigrateLegacyLists()
    {
        if (sequences.Count > 0 || (objects1.Count == 0 && objects2.Count == 0))
            return;

        if (objects1.Count > 0)
            sequences.Add(new ObjectSequence { name = "List 1", objects = objects1 });

        if (objects2.Count > 0)
            sequences.Add(new ObjectSequence { name = "List 2", objects = objects2 });
    }
}
