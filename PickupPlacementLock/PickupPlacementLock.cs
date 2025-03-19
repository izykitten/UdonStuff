using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class PickupPlacementLock : UdonSharpBehaviour
{
    // Position stored at edit time
    [Header("Position Preset Settings")]
    [SerializeField] private bool usePositionPreset = true;
    [SerializeField] private Vector3 presetPosition;
    [SerializeField] private Vector3 presetRotation;
    
    [Header("Behavior Settings")]
    // Set this to true to only snap back when dropped near the original position
    public bool snapOnlyWhenNearOrigin = false;
    // How close to the origin the object needs to be to snap back (in meters)
    public float snapThreshold = 1.0f;
    // Set to true to automatically freeze/unfreeze the rigidbody based on distance
    public bool autoManageConstraints = true;
    
    // Network sync variables
    [UdonSynced] private Vector3 syncedPosition;
    [UdonSynced] private Quaternion syncedRotation;
    [UdonSynced] private bool syncedIsPickedUp = false;
    [UdonSynced] private int syncedConstraints = (int)RigidbodyConstraints.FreezeAll;  // Changed to int
    
    private Vector3 defaultPosition;
    private Quaternion defaultRotation;
    private Rigidbody rb;
    private bool isPickedUp = false;
    private bool isBeingHeldByLocalPlayer = false;
    private float syncDelay = 0.1f; // Sync update interval
    private float lastSyncTime = 0;

    void Start()
    {
        if (usePositionPreset)
        {
            defaultPosition = presetPosition;
            defaultRotation = Quaternion.Euler(presetRotation);
        }
        else
        {
            defaultPosition = transform.position;
            defaultRotation = transform.rotation;
        }
        
        rb = GetComponent<Rigidbody>();
        
        // Initialize constraints
        if (rb != null && autoManageConstraints)
        {
            rb.constraints = RigidbodyConstraints.FreezeAll;
            syncedConstraints = (int)RigidbodyConstraints.FreezeAll;
        }
        
        syncedPosition = transform.position;
        syncedRotation = transform.rotation;
    }
    
    void Update()
    {
        // If we own the object, update the synced variables
        if (Networking.IsOwner(gameObject))
        {
            // Only update synced data periodically to reduce network traffic
            if (Time.time - lastSyncTime > syncDelay)
            {
                syncedPosition = transform.position;
                syncedRotation = transform.rotation;
                
                if (rb != null)
                {
                    syncedConstraints = (int)rb.constraints;
                }
                
                RequestSerialization();
                lastSyncTime = Time.time;
            }
            
            if (!isPickedUp && rb != null && autoManageConstraints)
            {
                float distance = Vector3.Distance(transform.position, defaultPosition);
                
                // If outside threshold, remove constraints
                if (distance > snapThreshold)
                {
                    rb.constraints = RigidbodyConstraints.None;
                }
                // If inside threshold, apply constraints
                else
                {
                    rb.constraints = RigidbodyConstraints.FreezeAll;
                }
            }
        }
        // If we don't own the object, apply the synced values (except when locally held)
        else if (!isBeingHeldByLocalPlayer)
        {
            transform.position = syncedPosition;
            transform.rotation = syncedRotation;
            
            if (rb != null)
            {
                rb.constraints = (RigidbodyConstraints)syncedConstraints;
            }
        }
    }
    
    public override void OnPickup()
    {
        // Take ownership when picked up
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        
        isPickedUp = true;
        syncedIsPickedUp = true;
        isBeingHeldByLocalPlayer = true;
        
        // Always remove constraints when picked up
        if (rb != null && autoManageConstraints)
        {
            rb.constraints = RigidbodyConstraints.None;
            syncedConstraints = (int)RigidbodyConstraints.None;
            RequestSerialization(); // Ensure constraints are synced immediately
        }
    }
    
    public override void OnDrop()
    {
        isPickedUp = false;
        syncedIsPickedUp = false;
        isBeingHeldByLocalPlayer = false;
        
        bool shouldSnapBack = true;
        
        if (snapOnlyWhenNearOrigin)
        {
            // Calculate distance between current position and default position
            float distance = Vector3.Distance(transform.position, defaultPosition);
            
            // Only snap back if the object is within the threshold distance
            shouldSnapBack = distance <= snapThreshold;
        }
        
        // Reset the object to its default position and rotation if needed
        if (shouldSnapBack)
        {
            transform.position = defaultPosition;
            transform.rotation = defaultRotation;
            
            // Apply constraints when at default position
            if (rb != null && autoManageConstraints)
            {
                rb.constraints = RigidbodyConstraints.FreezeAll;
                syncedConstraints = (int)RigidbodyConstraints.FreezeAll;
            }
        }
        else if (rb != null && autoManageConstraints)
        {
            // Keep constraints disabled if we're beyond the threshold
            rb.constraints = RigidbodyConstraints.None;
            syncedConstraints = (int)RigidbodyConstraints.None;
        }
        
        RequestSerialization();
    }
    
    // This method is called when synced variables are received from the network
    public override void OnDeserialization()
    {
        // Apply constraints even if we're not handling position/rotation
        if (!Networking.IsOwner(gameObject) && rb != null && !isBeingHeldByLocalPlayer)
        {
            rb.constraints = (RigidbodyConstraints)syncedConstraints;
        }
    }
    
    // Handle ownership changes
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        // When ownership changes and we're not the new owner, 
        // we're definitely not holding it locally anymore
        if (!Networking.IsOwner(gameObject))
        {
            isBeingHeldByLocalPlayer = false;
        }
    }
}