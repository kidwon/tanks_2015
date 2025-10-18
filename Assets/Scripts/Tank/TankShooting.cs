using UnityEngine;
using UnityEngine.UI;

public class TankShooting : MonoBehaviour
{
    public int m_PlayerNumber = 1;       
    public Rigidbody m_Shell;            
    public Transform m_FireTransform;    
    public Slider m_AimSlider;           
    public AudioSource m_ShootingAudio;  
    public AudioClip m_ChargingClip;     
    public AudioClip m_FireClip;         
    public float m_MinLaunchForce = 15f; 
    public float m_MaxLaunchForce = 30f; 
    public float m_MaxChargeTime = 0.75f;

    private string m_FireButton;         
    private float m_CurrentLaunchForce;  
    private float m_ChargeSpeed;         
    private bool m_Fired;                
    private bool m_UseExternalFireInput;
    private bool m_ExternalFirePressed;
    private bool m_ExternalFireHeld;
    private bool m_ExternalFireReleased;


    private void OnEnable()
    {
        m_CurrentLaunchForce = m_MinLaunchForce;
        m_AimSlider.value = m_MinLaunchForce;
    }


    private void Start()
    {
        m_FireButton = "Fire" + m_PlayerNumber;

        m_ChargeSpeed = (m_MaxLaunchForce - m_MinLaunchForce) / m_MaxChargeTime;
    }

    private void Update()
    {
        // Track the current state of the fire button and make decisions based on the current launch force.
        m_AimSlider.value = m_MinLaunchForce;
        bool firePressed;
        bool fireHeld;
        bool fireReleased;

        if (m_UseExternalFireInput)
        {
            firePressed = m_ExternalFirePressed;
            fireHeld = m_ExternalFireHeld;
            fireReleased = m_ExternalFireReleased;
            m_ExternalFirePressed = false;
            m_ExternalFireReleased = false;
        }
        else
        {
            firePressed = Input.GetButtonDown(m_FireButton);
            fireHeld = Input.GetButton(m_FireButton);
            fireReleased = Input.GetButtonUp(m_FireButton);
        }

        if (m_CurrentLaunchForce >= m_MaxLaunchForce && !m_Fired)
        {
            // at max charge, not yet fired
            m_CurrentLaunchForce = m_MaxLaunchForce;
            Fire();
        }
        else if (firePressed)
        {
            // have just pressed fire
            m_Fired = false;
            m_CurrentLaunchForce = m_MinLaunchForce;

            m_ShootingAudio.clip = m_ChargingClip;
            m_ShootingAudio.Play();
        }
        else if (fireHeld && !m_Fired)
        {
            // still holding fire
            m_CurrentLaunchForce += m_ChargeSpeed * Time.deltaTime;
            m_AimSlider.value = m_CurrentLaunchForce;
        }
        else if (fireReleased && !m_Fired)
        {
            // have released fire
            Fire();
        }
    }


    private void Fire()
    {
        // Instantiate and launch the shell.
        m_Fired = true;
        Rigidbody shellInstance = Instantiate(m_Shell, m_FireTransform.position, m_FireTransform.rotation);
        shellInstance.velocity = m_CurrentLaunchForce * m_FireTransform.forward;
        m_ShootingAudio.clip = m_FireClip;
        m_ShootingAudio.Play();
        m_CurrentLaunchForce = m_MinLaunchForce;
        m_AimSlider.value = m_MinLaunchForce;
    }

    public void SetExternalFireInput(bool pressed, bool held, bool released)
    {
        m_UseExternalFireInput = true;
        m_ExternalFirePressed = pressed;
        m_ExternalFireHeld = held;
        m_ExternalFireReleased = released;
    }

    public void DisableExternalFireInput()
    {
        m_UseExternalFireInput = false;
        m_ExternalFirePressed = false;
        m_ExternalFireHeld = false;
        m_ExternalFireReleased = false;
    }
}
