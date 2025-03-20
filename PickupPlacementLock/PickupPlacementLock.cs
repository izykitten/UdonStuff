using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[AddComponentMenu("izy/Pickups/Pickup Placement Lock")]
public class PickupPlacementLock : UdonSharpBehaviour
{
    [Header("Behavior Settings")]
    // Set this to true to only snap back when dropped near the original position
    public bool snapOnlyWhenNearOrigin = false;
    // How close to the origin the object needs to be to snap back (in meters)
    public float snapThreshold = 1.0f;
    
    private Vector3 defaultPosition;
    private Quaternion defaultRotation;
    private Rigidbody rb;
    private bool isPickedUp = false;

    void Start()
    {
        // Always use the current transform position and rotation
        defaultPosition = transform.position;
        defaultRotation = transform.rotation;
        
        rb = GetComponent<Rigidbody>();
        
        // Initialize constraints
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }
    }
    
    void Update()
    {
        if (!isPickedUp && rb != null)
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
    
    public override void OnPickup()
    {
        isPickedUp = true;
        
        // Always remove constraints when picked up
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.None;
        }
    }
    
    public override void OnDrop()
    {
        isPickedUp = false;
        
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
            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints.FreezeAll;
            }
        }
        else if (rb != null)
        {
            // Keep constraints disabled if we're beyond the threshold
            rb.constraints = RigidbodyConstraints.None;
        }
    }
}