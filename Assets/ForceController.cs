using UnityEngine;
using System.Collections;

namespace BGE.Forms
{
    public class ForceController : MonoBehaviour {
        public Camera headCamera;
        public float speed = 10.0f;

        public bool lookEnabled = true;
        public bool moveEnabled = true;
     
        Rigidbody rigidBody;

        [HideInInspector]
        public bool rotating = false;

        [HideInInspector]
        public bool attachedToCreature = false;

        public enum CameraType { free, forward };

        public CameraType cameraType = CameraType.forward;

        public float angularSpeed = 30.0f;

        public bool addFlow = true;
        public float flowForce = 1000;
        public float flowScale = 0.01f;

        public bool allowPitch = false;

        // Use this for initialization
        void Start()
        {
            //Cursor.lockState = CursorLockMode.Locked;
            rigidBody = GetComponent<Rigidbody>();
            rigidBody.freezeRotation = true;        

            desiredRotation = transform.rotation;
            headCamera = Camera.main;
            cameraType = CameraType.forward;
        }

        public Quaternion desiredRotation;

        void Yaw(float angle)
        {
            //rigidBody.AddTorque(Vector3.up * angle * 150);
            Quaternion rot = Quaternion.AngleAxis(angle, Vector3.up);
            desiredRotation = rot * desiredRotation;
            rotating = true;
            //transform.rotation = rot * transform.rotation;
        }

        void Roll(float angle)
        {
            //Quaternion rot = Quaternion.AngleAxis(angle, Vector3.forward);
            //transform.rotation = rot * transform.rotation;

            Quaternion rot = Quaternion.AngleAxis(angle, transform.forward);
            desiredRotation = rot * desiredRotation;

        }


        void Pitch(float angle)
        {
            float invcosTheta1 = Vector3.Dot(transform.forward, Vector3.up);
            float threshold = 0.95f;
            if ((angle > 0 && invcosTheta1 < (-threshold)) || (angle < 0 && invcosTheta1 > (threshold)))
            {
                //return;
            }

            // A pitch is a rotation around the right vector

            Vector3 right = desiredRotation* Vector3.right;
            Quaternion rot = Quaternion.AngleAxis(angle, right);
            desiredRotation = rot * desiredRotation;
            rotating = true;
        
            //Quaternion rot = Quaternion.AngleAxis(angle, transform.right);
            //transform.rotation = rot * transform.rotation;
        
    
        }

        void Walk(float units)
        {
            if (headCamera != null)
            {
                rigidBody.AddForce(headCamera.transform.forward* units);
            }
            else
            {
                rigidBody.AddForce(transform.forward* units);
            }
        }

        void Fly(float units)
        {
            rigidBody.AddForce(transform.up * units);     
        }

        void Strafe(float units)
        {
            if (headCamera != null)
            {
                rigidBody.AddForce(headCamera.transform.right* units);
            }
            else
            {
                rigidBody.AddForce(transform.right * units);
            }
        }

        int test = 0;

        // Update is called once per frame
        void FixedUpdate()
        {

            rotating = false;
            float mouseX, mouseY;
            float contSpeed = this.speed;
            float contAngularSpeed = this.angularSpeed;

            float runAxis = Input.GetAxis("Fire1");

            if (Input.GetKey(KeyCode.LeftShift) || runAxis != 0)
            {
                contSpeed *= 3f;
                contAngularSpeed *= 2.0f;
            }

            mouseX = Input.GetAxis("Mouse X");
            mouseY = Input.GetAxis("Mouse Y");

            if (mouseX != 0)
            {
                Yaw(mouseX * Time.deltaTime * contAngularSpeed);
            }
            else if (mouseY != 0 && allowPitch /*&& !UnityEngine.XR.XRDevice.isPresent*/)
            {
                Pitch(-mouseY * Time.deltaTime * contAngularSpeed);
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime);

            float joyX = Input.GetAxis("Joy X");
            float joyY = Input.GetAxis("Joy Y");

            if (Mathf.Abs(joyY) > 0.1f)
            {
                if (allowPitch /* && !UnityEngine.XR.XRDevice.isPresent*/)
                {
                    Pitch(-joyY * contAngularSpeed * Time.deltaTime);
                }
            }
            if (Mathf.Abs(joyX) > 0.3f)
            {
                Yaw(joyX * contAngularSpeed * Time.deltaTime);
            }
            if (Input.GetKey(KeyCode.E))
            {
                Fly(Time.deltaTime * speed);
            }
            if (Input.GetKey(KeyCode.F))
            {
                Fly(-Time.deltaTime * speed);
            }

            //Yaw(joyX * angularSpeed * Time.deltaTime);
            //Fly(-joyY * contSpeed * Time.deltaTime);

            float contWalk = Input.GetAxis("Vertical");
            float contStrafe = Input.GetAxis("Horizontal");
            if (Mathf.Abs(contWalk) > 0.1f && moveEnabled)
            {
                Walk(contWalk * contSpeed * Time.deltaTime);
            }
            if (Mathf.Abs(contStrafe) > 0.1f && moveEnabled)
            {
                Strafe(contStrafe * contSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.Joystick1Button5))
            {
                Fly(contSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.Joystick1Button4))
            {
                Fly(-contSpeed * Time.deltaTime);
            }

            if (addFlow)
            {
                Vector3 p = transform.position;
                float n = Mathf.PerlinNoise(p.x * flowScale, p.z * flowScale);
                Vector3 force = Quaternion.AngleAxis(
                    Utilities.Map(n, 0, 1, -180, 180)
                    , Vector3.up)
                    * Vector3.forward
                    * flowForce
                    ;
                force += Quaternion.AngleAxis(
                    Utilities.Map(n, 0, 1, -180, 180)
                    , Vector3.right)
                    * Vector3.forward
                    * flowForce
                    ;

                rigidBody.AddForce(force);                
            }

            if (transform.position.y > 20000)
            {
                Vector3 p = transform.position;
                p.y = 20000;
                transform.position = p;
                desiredRotation = Quaternion.AngleAxis(-180, Vector3.right) * desiredRotation;
                transform.rotation = desiredRotation;
                GetComponent<Rigidbody>().velocity = -GetComponent<Rigidbody>().velocity;
            }
        }

        
        private void OnDrawGizmos()
        {
            //if (!Application.isEditor)
            {
                Gizmos.color = Color.green;
                Vector3 p = transform.position;

                float n = Mathf.PerlinNoise(p.x * flowScale, p.z * flowScale);
                Vector3 force = Quaternion.AngleAxis(
                    Utilities.Map(n, 0, 1, -180, 180)
                    , Vector3.up)
                    * Vector3.forward
                    * flowForce
                    ;
                force += Quaternion.AngleAxis(
                    Utilities.Map(n, 0, 1, -180, 180)
                    , Vector3.right)
                    * Vector3.forward
                    * flowForce
                    ;

                Gizmos.DrawLine(transform.position, transform.position + force);
            }
        }

       
        void Update()
        {
            
            if (Input.GetKeyDown(KeyCode.JoystickButton2))
            {
                int ct = ((int)(cameraType + 1) % System.Enum.GetNames(typeof(CameraType)).Length);
                cameraType = (CameraType)ct;
            }
            //Pitch(angularSpeed * Time.deltaTime);
        }
    }
}