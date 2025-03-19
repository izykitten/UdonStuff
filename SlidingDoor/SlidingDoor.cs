using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public enum DoorState
{
    Closed,
    Opening,
    Open,
    Closing
}

public class SlidingDoor : UdonSharpBehaviour
{
    [SerializeField]
    private Transform leftDoor;

    [SerializeField]
    private Transform rightDoor;

    // The distance each door moves when opening.
    [SerializeField]
    private Vector3 leftOpenOffset = new Vector3(-1f, 0f, 0f);

    [SerializeField]
    private Vector3 rightOpenOffset = new Vector3(1f, 0f, 0f);

    // Duration for the door to open or close.
    [SerializeField]
    private float openCloseTime = 1.0f;

    private Vector3 leftClosedPos;
    private Vector3 rightClosedPos;
    private float timer;

    private DoorState doorState = DoorState.Closed;

    void Start()
    {
        // Store the original positions as the closed state.
        leftClosedPos = leftDoor.localPosition;
        rightClosedPos = rightDoor.localPosition;
    }

    // Toggle the door state between open and closed.
    public void ToggleDoor()
    {
        if (doorState == DoorState.Open)
        {
            StartClosing();
        }
        else if (doorState == DoorState.Closed)
        {
            StartOpening();
        }
    }

    // Closes the door when _keypadClosed is received.
    public void _keypadClosed()
    {
        if (doorState == DoorState.Open)
        {
            StartClosing();
        }
    }

    public override void Interact()
    {
        if (doorState == DoorState.Closed)
        {
            StartOpening();
        }
    }

    void Update()
    {
        if (doorState == DoorState.Opening)
        {
            UpdateDoorPosition(leftClosedPos, leftClosedPos + leftOpenOffset,
                               rightClosedPos, rightClosedPos + rightOpenOffset, DoorState.Open);
        }
        else if (doorState == DoorState.Closing)
        {
            UpdateDoorPosition(leftClosedPos + leftOpenOffset, leftClosedPos,
                               rightClosedPos + rightOpenOffset, rightClosedPos, DoorState.Closed);
        }
    }

    private void StartOpening()
    {
        doorState = DoorState.Opening;
        timer = 0;
    }

    private void StartClosing()
    {
        doorState = DoorState.Closing;
        timer = 0;
    }

    private void UpdateDoorPosition(Vector3 leftStart, Vector3 leftTarget, Vector3 rightStart, Vector3 rightTarget, DoorState finalState)
    {
        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / openCloseTime);
        leftDoor.localPosition = Vector3.Lerp(leftStart, leftTarget, t);
        rightDoor.localPosition = Vector3.Lerp(rightStart, rightTarget, t);

        if (t >= 1f)
        {
            doorState = finalState;
        }
    }
}