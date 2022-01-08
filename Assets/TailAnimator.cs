using UnityEngine;
using System.Collections;
using BGE.Forms;

namespace BGE.Forms
{
    public class TailAnimator : BGE.Forms.Animator {
        public enum Axis { X, Y, Z };
        public enum DriverType { velocity, acceleration, force };

        public DriverType driver = DriverType.acceleration;
        public Axis axis = Axis.Y;

        public float theta = 0;
        public float amplitude = 40.0f;
        public float speed = 0.1f;
        // Use this for initialization
        void Start()
        {
            theta = 0;
        }

        private Renderer renderer = null;

        // Update is called once per frame
        void Update()
        {
            if (boid == null) return;

            if (suspended)
            {
                return;
            }

            float angle = Mathf.Sin(theta) * amplitude;
            switch (axis)
            {
                case Axis.X:
                    transform.localRotation = Quaternion.Euler(angle, 0, 0);
                    break;
                case Axis.Y:
                    transform.localRotation = Quaternion.Euler(0, angle, 0);
                    break;
                case Axis.Z:
                    transform.localRotation = Quaternion.Euler(0, 0, angle);
                    break;
            }
            switch (driver)
            {
                /*
                case DriverType.velocity:
                    theta += speed * Time.deltaTime * boid.velocity.magnitude;
                    break;
                case DriverType.acceleration:
                    theta += speed * Time.deltaTime * boid.acceleration.magnitude;
                    break;
                case DriverType.force:
                    theta += speed * Time.deltaTime * boid.force.magnitude;
                    break;
                */
            }
            
        }
    }
}