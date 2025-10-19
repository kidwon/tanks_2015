using UnityEngine;

public class TankMovement : MonoBehaviour
{
    public int m_PlayerNumber = 1;         
    public float m_Speed = 12f;            
    public float m_TurnSpeed = 180f;       
    public AudioSource m_MovementAudio;    
    public AudioClip m_EngineIdling;       
    public AudioClip m_EngineDriving;      
    public float m_PitchRange = 0.2f;

    
    private string m_MovementAxisName;     
    private string m_TurnAxisName;         
    private Rigidbody m_Rigidbody;         
    private float m_MovementInputValue;    
    private float m_TurnInputValue;        
    private float m_OriginalPitch;         
    private bool m_UseExternalInput;
    private float m_ExternalMovement;
    private float m_ExternalTurn;
    private bool m_HasMoveTarget;
    private Vector3 m_MoveTarget;
    private float m_MoveTargetTolerance = 1.5f;


    private void Awake()
    {
        m_Rigidbody = GetComponent<Rigidbody>();
    }


    private void OnEnable ()
    {
        m_Rigidbody.isKinematic = false;
        m_MovementInputValue = 0f;
        m_TurnInputValue = 0f;
    }


    private void OnDisable ()
    {
        m_Rigidbody.isKinematic = true;
    }


    private void Start()
    {
        m_MovementAxisName = "Vertical" + m_PlayerNumber;
        m_TurnAxisName = "Horizontal" + m_PlayerNumber;

        m_OriginalPitch = m_MovementAudio.pitch;
    }


    private void Update()
    {
        // Store the player's input and make sure the audio for the engine is playing.
        float manualMove = Input.GetAxis(m_MovementAxisName);
        float manualTurn = Input.GetAxis(m_TurnAxisName);

        if (Mathf.Abs(manualMove) > 0.05f || Mathf.Abs(manualTurn) > 0.05f)
        {
            m_HasMoveTarget = false;
        }

        if (m_HasMoveTarget)
        {
            UpdateAutoMovement();
        }
        else if (m_UseExternalInput)
        {
            m_MovementInputValue = Mathf.Clamp(m_ExternalMovement, -1f, 1f);
            m_TurnInputValue = Mathf.Clamp(m_ExternalTurn, -1f, 1f);
        }
        else
        {
            m_MovementInputValue = manualMove;
            m_TurnInputValue = manualTurn;
        }
        EngineAudio();
    }

    public void SetExternalInput(float movement, float turn)
    {
        m_UseExternalInput = true;
        m_ExternalMovement = Mathf.Clamp(movement, -1f, 1f);
        m_ExternalTurn = Mathf.Clamp(turn, -1f, 1f);
    }

    public void DisableExternalInput()
    {
        m_UseExternalInput = false;
        m_ExternalMovement = 0f;
        m_ExternalTurn = 0f;
    }

    public void SetMoveTarget(Vector3 position, float stopDistance = 1.5f)
    {
        m_MoveTarget = position;
        m_MoveTargetTolerance = Mathf.Max(0.2f, stopDistance);
        m_HasMoveTarget = true;
        m_UseExternalInput = false;
    }

    public void ClearMoveTarget()
    {
        m_HasMoveTarget = false;
    }

    private void UpdateAutoMovement()
    {
        Vector3 target = m_MoveTarget;
        target.y = transform.position.y;

        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        if (distance <= m_MoveTargetTolerance)
        {
            m_HasMoveTarget = false;
            m_MovementInputValue = 0f;
            m_TurnInputValue = 0f;
            return;
        }

        if (toTarget.sqrMagnitude < Mathf.Epsilon)
        {
            m_HasMoveTarget = false;
            m_MovementInputValue = 0f;
            m_TurnInputValue = 0f;
            return;
        }

        Vector3 desiredDirection = toTarget.normalized;
        float turnAngle = Vector3.SignedAngle(transform.forward, desiredDirection, Vector3.up);
        float turnInput = Mathf.Clamp(turnAngle / 45f, -1f, 1f);

        float forwardDot = Mathf.Clamp01(Vector3.Dot(transform.forward, desiredDirection));
        float moveInput = forwardDot;

        if (Mathf.Abs(turnAngle) > 60f)
        {
            moveInput = 0f;
        }
        else if (Mathf.Abs(turnAngle) > 25f)
        {
            moveInput *= 0.5f;
        }

        m_MovementInputValue = moveInput;
        m_TurnInputValue = turnInput;
    }


    private void EngineAudio()
    {
        // Play the correct audio clip based on whether or not the tank is moving and what audio is currently playing.
        if (Mathf.Abs(m_MovementInputValue) < 0.1f && Mathf.Abs(m_TurnInputValue) < 0.1f)
        {
            if (m_MovementAudio.clip == m_EngineDriving)
            {
                m_MovementAudio.clip = m_EngineIdling;
                m_MovementAudio.pitch = Random.Range(m_OriginalPitch - m_PitchRange, m_OriginalPitch + m_PitchRange);
                m_MovementAudio.Play();
            }
        }
        else
        {
            if (m_MovementAudio.clip == m_EngineIdling)
            {
                m_MovementAudio.clip = m_EngineDriving;
                m_MovementAudio.pitch = Random.Range(m_OriginalPitch - m_PitchRange, m_OriginalPitch + m_PitchRange);
                m_MovementAudio.Play();
            }
        }
    }


    private void FixedUpdate()
    {
        // Move and turn the tank.
        Move();
        Turn();
    }


    private void Move()
    {
        // Adjust the position of the tank based on the player's input.
        Vector3 movement = transform.forward * m_MovementInputValue * m_Speed * Time.deltaTime;
        m_Rigidbody.MovePosition(m_Rigidbody.position + movement);
    }


    private void Turn()
    {
        // Adjust the rotation of the tank based on the player's input.
        float turn = m_TurnInputValue * m_TurnSpeed * Time.deltaTime;
        Quaternion turnRotation = Quaternion.Euler(0f, turn, 0f);
        m_Rigidbody.MoveRotation(m_Rigidbody.rotation * turnRotation);
    }
}
