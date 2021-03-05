using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.SceneManagement;

namespace ew
{
    public class BoidBootstrap : MonoBehaviour
    {

        public static int MAX_BOIDS = 25000;
        public static int MAX_SPINES = 50;
        public static int MAX_NEIGHBOURS = 150;

        private EntityArchetype boidArchitype;
        private EntityArchetype headArchitype;
        private EntityArchetype tailArchitype;
        private EntityArchetype spineArchitype;

        private EntityManager entityManager;

        private RenderMesh bodyMesh;
        public Mesh mesh;
        public Material material;

        public float seperationWeight = 1.0f;
        public float cohesionWeight = 2.0f;
        public float alignmentWeight = 1.0f;
        public float wanderWeight = 1.0f;
        public float baseConstrainWeight = 1.0f;
        public float constrainWeight = 1.0f;

        public float fleeWeight = 1.0f;
        public float fleeDistance = 50;

        public float headAmplitude = 20;
        public float tailAmplitude = 30;
        public float animationFrequency = 1;

        public int totalNeighbours = 50;

        public bool threedcells = false;

        public int spineLength = 4;
        public float bondDamping = 10;
        public float angularDamping = 10;

        public float limitUpAndDown = 0.5f;

        public int maxBoidsPerFrame = 500;

        public float seekWeight = 0;

        public Vector3 constrainTranslation;

        NativeArray<Entity> allTheBoids;
        NativeArray<Entity> allTheheadsAndTails;
        NativeArray<Entity> allTheSpines;

        BoidJobSystem boidJobSystem;

        public Coroutine cr;

        public static float Map(float value, float r1, float r2, float m1, float m2)
        {
            float dist = value - r1;
            float range1 = r2 - r1;
            float range2 = m2 - m1;
            return m1 + ((dist / range1) * range2);
        }

        public void OnDestroy()
        {
            Debug.Log("OnDestroy BoidBootstrap");

            if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
            {
                Debug.Log("Destroying the entities");
                entityManager.DestroyEntity(allTheBoids);
                entityManager.DestroyEntity(allTheheadsAndTails);
                entityManager.DestroyEntity(allTheSpines);
                BoidJobSystem.Instance.Enabled = false;
                SpineSystem.Instance.Enabled = false;
                HeadsAndTailsSystem.Instance.Enabled = false;
            }    
            allTheBoids.Dispose();
            allTheheadsAndTails.Dispose();
            allTheSpines.Dispose();
        }

        Entity CreateSmallBoid(Vector3 pos, Quaternion q, int boidId, float size)
        {
            Entity boidEntity = entityManager.CreateEntity(boidArchitype);
            allTheBoids[boidId] = boidEntity;

            Translation p = new Translation();
            p.Value = pos;

            Rotation r = new Rotation();
            r.Value = q;

            entityManager.SetComponentData(boidEntity, p);
            entityManager.SetComponentData(boidEntity, r);

            NonUniformScale s = new NonUniformScale();
            s.Value = new Vector3(size * 0.5f, size, size);
            //s.Value = new Vector3(2, 4, 10);

            entityManager.SetComponentData(boidEntity, s);


            entityManager.SetComponentData(boidEntity, new Boid() { boidId = boidId, mass = 1, maxSpeed = 100, maxForce = 400, weight = 200 });
            entityManager.SetComponentData(boidEntity, new Seperation());
            entityManager.SetComponentData(boidEntity, new Alignment());
            entityManager.SetComponentData(boidEntity, new Cohesion());
            entityManager.SetComponentData(boidEntity, new Constrain());
            entityManager.SetComponentData(boidEntity, new Flee());
            entityManager.SetComponentData(boidEntity, new Wander()
            {
                distance = 2
                ,
                radius = 1.2f,
                jitter = 80,
                target = UnityEngine.Random.insideUnitSphere * 1.2f
            });
            entityManager.SetComponentData(boidEntity, new Spine() { parent = -1, spineId = boidId });

            entityManager.SetComponentData(boidEntity, new ObstacleAvoidance() {forwardFeelerDepth = 50, forceType = ObstacleAvoidance.ForceType.normal });
            

            entityManager.AddSharedComponentData(boidEntity, bodyMesh);

            // Make the head
            Entity headEntity = entityManager.CreateEntity(headArchitype);
            allTheheadsAndTails[boidId * 2] = headEntity;

            Translation headTranslation = new Translation();
            headTranslation.Value = pos + (q * Vector3.forward) * size;
            entityManager.SetComponentData(headEntity, headTranslation);
            Rotation headRotation = new Rotation();
            headRotation.Value = q;
            entityManager.SetComponentData(headEntity, headRotation);
            entityManager.AddSharedComponentData(headEntity, bodyMesh);
            entityManager.SetComponentData(headEntity, s);
            entityManager.SetComponentData(headEntity, new Head() { boidId = boidId, spineId = boidId });

            // End head

            // Make the tail
            Entity tailEntity = entityManager.CreateEntity(tailArchitype);
            allTheheadsAndTails[(boidId * 2) + 1] = tailEntity;

            Translation tailTranslation = new Translation();
            tailTranslation.Value = pos - (q * Vector3.forward) * size;
            entityManager.SetComponentData(tailEntity, tailTranslation);
            Rotation tailRotation = new Rotation();
            tailRotation.Value = q;
            entityManager.SetComponentData(tailEntity, tailRotation);
            entityManager.AddSharedComponentData(tailEntity, bodyMesh);
            entityManager.SetComponentData(tailEntity, s);
            entityManager.SetComponentData(tailEntity, new Tail() { boidId = boidId, spineId = boidId });
            // End tail

            return boidEntity;
        }

        Entity Create4PartBoid(Vector3 pos, Quaternion q, int boidId, float size, int parts)
        {
            Entity boidEntity = entityManager.CreateEntity(boidArchitype);
            allTheBoids[boidId] = boidEntity;
            Translation p = new Translation
            {
                Value = pos
            };

            Rotation r = new Rotation
            {
                Value = q
            };

            entityManager.SetComponentData(boidEntity, p);
            entityManager.SetComponentData(boidEntity, r);

            NonUniformScale s = new NonUniformScale
            {
                Value = new Vector3(size * 0.3f, size, size)
            };

            entityManager.SetComponentData(boidEntity, s);

            entityManager.SetComponentData(boidEntity, new Boid() { boidId = boidId, mass = 1, maxSpeed = 100 * UnityEngine.Random.Range(0.9f, 1.1f), maxForce = 400, weight = 200 });
            entityManager.SetComponentData(boidEntity, new Seperation());
            entityManager.SetComponentData(boidEntity, new Alignment());
            entityManager.SetComponentData(boidEntity, new Cohesion());
            entityManager.SetComponentData(boidEntity, new Constrain());
            entityManager.SetComponentData(boidEntity, new Flee());
            entityManager.SetComponentData(boidEntity, new Wander()
            {
                distance = 2
                ,
                radius = 1.2f,
                jitter = 80,
                target = UnityEngine.Random.insideUnitSphere * 1.2f
            });                        
            entityManager.SetComponentData(boidEntity, new Spine() { parent = -1, spineId = (spineLength + 1) * boidId });

            entityManager.AddSharedComponentData(boidEntity, bodyMesh);

            for (int i = 0; i < parts; i++)
            {
                int parentId = (boidId * (spineLength + 1)) + i;
                Translation sp = new Translation
                {
                    Value = pos - (q * Vector3.forward) * size * (float)(i + 1)
                };
                Entity spineEntity = entityManager.CreateEntity(spineArchitype);
                int spineIndex = (boidId * spineLength) + i;
                allTheSpines[spineIndex] = spineEntity;

                entityManager.SetComponentData(spineEntity, sp);
                entityManager.SetComponentData(spineEntity, r);
                entityManager.SetComponentData(spineEntity, new Spine() { parent = parentId, spineId = parentId + 1, offset = new Vector3(0, 0, -size) });
                entityManager.AddSharedComponentData(spineEntity, bodyMesh);
                s = new NonUniformScale
                {
                    Value = new Vector3(0.01f, Map(i, 0, spineLength, size, 0.01f * size), size)
                };
                //s.Value = new Vector3(2, 4, 10);
                entityManager.SetComponentData(spineEntity, s);

            }

            // Make the head

            Entity headEntity = entityManager.CreateEntity(headArchitype);
            allTheheadsAndTails[boidId * 2] = headEntity;
            Translation headTranslation = new Translation();
            headTranslation.Value = pos + (q * Vector3.forward) * size;
            entityManager.SetComponentData(headEntity, headTranslation);
            Rotation headRotation = new Rotation();
            headRotation.Value = q;
            entityManager.SetComponentData(headEntity, headRotation);
            entityManager.AddSharedComponentData(headEntity, bodyMesh);
            entityManager.SetComponentData(headEntity, s);
            s = new NonUniformScale
            {
                Value = new Vector3(size * 0.3f, size, size)
            };
            //s.Value = new Vector3(2, 4, 10);
            entityManager.SetComponentData(headEntity, s);
            entityManager.SetComponentData(headEntity, new Head() { spineId = boidId * (spineLength + 1), boidId = boidId });
            // End head

            // Make the tail
            Entity tailEntity = entityManager.CreateEntity(tailArchitype);
            allTheheadsAndTails[(boidId * 2) + 1] = tailEntity;
            Translation tailTranslation = new Translation();
            tailTranslation.Value = pos - (q * Vector3.forward) * size;
            //tailTranslation.Value = pos - (q * Vector3.forward) * size * (spineLength + 2);
            entityManager.SetComponentData(tailEntity, tailTranslation);
            Rotation tailRotation = new Rotation();
            tailRotation.Value = q;
            s = new NonUniformScale
            {
                Value = new Vector3(size * 0.3f, size, size)
            };
            //s.Value = new Vector3(2, 4, 10);
            entityManager.SetComponentData(tailEntity, s);
            entityManager.SetComponentData(tailEntity, tailRotation);
            entityManager.AddSharedComponentData(tailEntity, bodyMesh);
            entityManager.SetComponentData(tailEntity, s);
            entityManager.SetComponentData(tailEntity, new Tail() { boidId = boidId, spineId = boidId * (spineLength + 1) });
            // End tail    

            return boidEntity;


        }


        Entity CreateBoidWithTrail(Vector3 pos, Quaternion q, int boidId, float size)
        {
            Entity boidEntity = entityManager.CreateEntity(boidArchitype);
            allTheBoids[boidId] = boidEntity;
            Translation p = new Translation
            {
                Value = pos
            };

            Rotation r = new Rotation
            {
                Value = q
            };

            entityManager.SetComponentData(boidEntity, p);
            entityManager.SetComponentData(boidEntity, r);

            NonUniformScale s = new NonUniformScale
            {
                Value = new Vector3(size * 0.3f, size, size)
            };

            entityManager.SetComponentData(boidEntity, s);

            entityManager.SetComponentData(boidEntity, new Boid() { boidId = boidId, mass = 1, maxSpeed = 100 * UnityEngine.Random.Range(0.9f, 1.1f), maxForce = 400, weight = 200 });
            entityManager.SetComponentData(boidEntity, new Seperation());
            entityManager.SetComponentData(boidEntity, new Alignment());
            entityManager.SetComponentData(boidEntity, new Cohesion());
            entityManager.SetComponentData(boidEntity, new Constrain());
            entityManager.SetComponentData(boidEntity, new Flee());
            entityManager.SetComponentData(boidEntity, new Wander()
            {
                distance = 2
                ,
                radius = 1.2f,
                jitter = 80,
                target = UnityEngine.Random.insideUnitSphere * 1.2f
            });                        
            entityManager.SetComponentData(boidEntity, new Spine() { parent = -1, spineId = (spineLength + 1) * boidId });
            entityManager.SetComponentData(boidEntity, new ObstacleAvoidance() {forwardFeelerDepth = 50, forceType = ObstacleAvoidance.ForceType.normal});

            entityManager.AddSharedComponentData(boidEntity, bodyMesh);

            for (int i = 0; i < spineLength; i++)
            {
                int parentId = (boidId * (spineLength + 1)) + i;
                Translation sp = new Translation
                {
                    Value = pos - (q * Vector3.forward) * size * (float)(i + 1)
                };
                Entity spineEntity = entityManager.CreateEntity(spineArchitype);
                int spineIndex = (boidId * spineLength) + i;
                allTheSpines[spineIndex] = spineEntity;

                entityManager.SetComponentData(spineEntity, sp);
                entityManager.SetComponentData(spineEntity, r);
                entityManager.SetComponentData(spineEntity, new Spine() { parent = parentId, spineId = parentId + 1, offset = new Vector3(0, 0, -size) });
                entityManager.AddSharedComponentData(spineEntity, bodyMesh);
                s = new NonUniformScale
                {
                    Value = new Vector3(0.01f, Map(i, 0, spineLength, size, 0.01f * size), size)
                };
                //s.Value = new Vector3(2, 4, 10);
                entityManager.SetComponentData(spineEntity, s);

            }

            // Make the head

            Entity headEntity = entityManager.CreateEntity(headArchitype);
            allTheheadsAndTails[boidId * 2] = headEntity;
            Translation headTranslation = new Translation();
            headTranslation.Value = pos + (q * Vector3.forward) * size;
            entityManager.SetComponentData(headEntity, headTranslation);
            Rotation headRotation = new Rotation();
            headRotation.Value = q;
            entityManager.SetComponentData(headEntity, headRotation);
            entityManager.AddSharedComponentData(headEntity, bodyMesh);
            entityManager.SetComponentData(headEntity, s);
            s = new NonUniformScale
            {
                Value = new Vector3(size * 0.3f, size, size)
            };
            //s.Value = new Vector3(2, 4, 10);
            entityManager.SetComponentData(headEntity, s);
            entityManager.SetComponentData(headEntity, new Head() { spineId = boidId * (spineLength + 1), boidId = boidId });
            // End head

            // Make the tail
            Entity tailEntity = entityManager.CreateEntity(tailArchitype);
            allTheheadsAndTails[(boidId * 2) + 1] = tailEntity;
            Translation tailTranslation = new Translation();
            tailTranslation.Value = pos - (q * Vector3.forward) * size;
            //tailTranslation.Value = pos - (q * Vector3.forward) * size * (spineLength + 2);
            entityManager.SetComponentData(tailEntity, tailTranslation);
            Rotation tailRotation = new Rotation();
            tailRotation.Value = q;
            s = new NonUniformScale
            {
                Value = new Vector3(size * 0.3f, size, size)
            };
            //s.Value = new Vector3(2, 4, 10);
            entityManager.SetComponentData(tailEntity, s);
            entityManager.SetComponentData(tailEntity, tailRotation);
            entityManager.AddSharedComponentData(tailEntity, bodyMesh);
            entityManager.SetComponentData(tailEntity, s);
            entityManager.SetComponentData(tailEntity, new Tail() { boidId = boidId, spineId = boidId * (spineLength + 1) });
            // End tail    

            return boidEntity;
        }

        public int numBoids = 100;
        public float radius = 2000;
        public float neighbourDistance = 20;

        [Range(0.0f, 10.0f)]
        public float speed = 1.0f;

        public bool isContainer = false;

        // Start is called before the first frame update
        void Start()
        {
            BoidJobSystem.Instance.Enabled = true;
            HeadsAndTailsSystem.Instance.Enabled = true;
            SpineSystem.Instance.Enabled = true;
            allTheBoids = new NativeArray<Entity>(numBoids, Allocator.Persistent);
            allTheheadsAndTails = new NativeArray<Entity>(numBoids * 2, Allocator.Persistent);
            allTheSpines = new NativeArray<Entity>(numBoids * spineLength, Allocator.Persistent);

            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            constrainTranslation = transform.position;
            Cursor.visible = false;
            constrainWeight = baseConstrainWeight;

            boidArchitype = entityManager.CreateArchetype(
                typeof(Translation),
                typeof(Rotation),
                typeof(NonUniformScale),
                typeof(LocalToWorld),
                typeof(RenderBounds),
                typeof(Boid),
                typeof(Seperation),
                typeof(Cohesion),
                typeof(Alignment),
                typeof(Wander),
                typeof(Constrain),
                typeof(Flee),
                typeof(Seek),
                typeof(ObstacleAvoidance),
                typeof(Spine)

            );

            headArchitype = entityManager.CreateArchetype(
                typeof(Translation),
                typeof(Rotation),
                typeof(NonUniformScale),
                typeof(LocalToWorld),
                typeof(RenderBounds),
                typeof(Head)
                );

            tailArchitype = entityManager.CreateArchetype(
                        typeof(Translation),
                        typeof(Rotation),
                        typeof(NonUniformScale),
                        typeof(LocalToWorld),
                        typeof(RenderBounds),
                        typeof(Tail)
                        );

            spineArchitype = entityManager.CreateArchetype(
                    typeof(Translation),
                    typeof(Rotation),
                    typeof(NonUniformScale),
                    typeof(LocalToWorld),
                    typeof(RenderBounds),
                    typeof(Spine)
                    );

            bodyMesh = new RenderMesh
            {
                mesh = mesh,
                material = material
            };
            StartCoroutine(CreateBoids());
            
            Cursor.visible = false;

            cr = StartCoroutine(Show());

            //Cursor.lockState = CursorLockMode.Locked;
        }

        IEnumerator CreateBoids()
        {
            int created = 0;
            BoidJobSystem.Instance.Enabled = true;
            SpineSystem.Instance.Enabled = true;
            HeadsAndTailsSystem.Instance.Enabled = true;
            while (created < numBoids)
            {
                Vector3 pos = UnityEngine.Random.insideUnitSphere * radius;
                Quaternion q = Quaternion.Euler(UnityEngine.Random.Range(-20, 20), UnityEngine.Random.Range(0, 360), 0);
                CreateBoidWithTrail(transform.position + pos, q, created, size);
                created++;
                if (created % maxBoidsPerFrame == 0)
                {
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }

        public float size = 3.0f;

        public int cellSize = 50;
        public int gridSize = 10000;
        public bool usePartitioning = true;

        Material boidMaterial;

        public float colorSpeed = 100;

        public float colorAdd = 0;

        public void Update()
        {
            if (isContainer)
                return;

            BoidJobSystem.Instance.bootstrap = this;
            //SpineSystem.Instance.bootstrap = this;
            //HeadsAndTailsSystem.Instance.bootstrap = this;

            /*if (Input.GetAxis("DPadX") == -1)
            {
                colorAdd = Mathf.Lerp(colorAdd, -colorSpeed, Time.deltaTime);
                material.SetFloat("_TranslationNonUniformScale", material.GetFloat("_TranslationNonUniformScale") + colorAdd);
            }

            if (Input.GetAxis("DPadX") == 1)
            {
                colorAdd = Mathf.Lerp(colorAdd, +colorSpeed, Time.deltaTime);
                material.SetFloat("_TranslationNonUniformScale", material.GetFloat("_TranslationNonUniformScale") + colorAdd);
            }
            */

            if (Input.GetAxis("DPadY") == -1)
            {
                colorAdd = Mathf.Lerp(colorAdd, -colorSpeed, Time.deltaTime);
            }

            if (Input.GetAxis("DPadY") == 1)
            {
                colorAdd = Mathf.Lerp(colorAdd, -colorSpeed, Time.deltaTime);
            }
            material.SetFloat("_Offset", material.GetFloat("_Offset") + colorAdd);
            colorAdd = Mathf.Lerp(colorAdd, 0, Time.deltaTime * 0.5f);


            //if (Input.GetKeyDown(KeyCode.Joystick1Button8))
            //{
            //    StartCoroutine(CreateBoids());

            //}
            //if (Input.GetKeyDown(KeyCode.Joystick1Button9))
            //{
            //    DestroyEntities();
            //}

            if (Input.GetKey(KeyCode.Joystick1Button2))
            {
                speed -= Time.deltaTime;
                if (speed < 0)
                {
                    speed = 0;
                }
            }

            if (Input.GetKey(KeyCode.Joystick1Button1))
            {
                speed += Time.deltaTime;
                if (speed > 5)
                {
                    speed = 5;
                }
            }
            Explosion();
        }

        float ellapsed = 1000;
        public float toPass = 0.3f;
        public int clickCount = 0;

        void Awake()
        {
            //SceneManager.sceneUnloaded += DestroyTheBoids;
        }

        void DoExplosion(int expType)
        {
            switch (expType)
            {
                case 1:
                    radius = 10;
                    totalNeighbours = 1;
                    limitUpAndDown = 1;
                    seekWeight = 0;
                    //constrainTranslation = Camera.main.transform.Translation;
                    break;
                case 2:
                    radius = 1000;
                    cohesionWeight = 0;
                    totalNeighbours = 100;
                    neighbourDistance = 100;
                    limitUpAndDown = 1;
                    seekWeight = 0;
                    constrainWeight = baseConstrainWeight;
                    break;
                case 3:
                    radius = 1300;
                    cohesionWeight = 0;
                    totalNeighbours = 100;
                    neighbourDistance = 100;
                    limitUpAndDown = 1;
                    seekWeight = 0;
                    constrainWeight = baseConstrainWeight;
                    break;
                case 4:
                    radius = 1500;
                    neighbourDistance = 150;
                    totalNeighbours = 100;
                    cohesionWeight = 0;
                    limitUpAndDown = 1;
                    seekWeight = 0;
                    constrainWeight = baseConstrainWeight;
                    break;
                case 5:
                    radius = 2000;
                    neighbourDistance = 0;
                    totalNeighbours = 100;
                    cohesionWeight = 0;
                    limitUpAndDown = 1;
                    seekWeight = 0;
                    constrainWeight = baseConstrainWeight;
                    break;
                case 6:
                    radius = 800;
                    neighbourDistance = 150;
                    totalNeighbours = 100;
                    cohesionWeight = 2;
                    limitUpAndDown = 0.9f;
                    seekWeight = 0;
                    constrainWeight = baseConstrainWeight;
                    break;
                case 7:
                    radius = 1000;
                    neighbourDistance = 150;
                    totalNeighbours = 100;
                    cohesionWeight = 2;
                    limitUpAndDown = 0.9f;
                    constrainWeight = baseConstrainWeight;
                    break;
                case 8:
                    radius = 1500;
                    neighbourDistance = 150;
                    totalNeighbours = 100;
                    cohesionWeight = 2;
                    limitUpAndDown = 0.9f;
                    seekWeight = 0;
                    constrainWeight = baseConstrainWeight;
                    break;
                case 9:
                    radius = 2000;
                    neighbourDistance = 150;
                    totalNeighbours = 100;
                    cohesionWeight = 2;
                    limitUpAndDown = 0.9f;
                    seekWeight = 0;
                    constrainWeight = baseConstrainWeight;
                    break;
                case 10:
                    seekWeight = 1;
                    radius = 2000;
                    neighbourDistance = 150;
                    totalNeighbours = 100;
                    fleeWeight = 3.0f;
                    cohesionWeight = 2;
                    constrainWeight = 0;
                    limitUpAndDown = 0.9f;
                    break;
            }
        }

        public IEnumerator Show()
        {
            while (true)
            {
                yield return new WaitForSeconds(30);
                DoExplosion(1);
                yield return new WaitForSeconds(UnityEngine.Random.Range(4, 6));
                int exp = UnityEngine.Random.Range(2, 10);
                DoExplosion(exp);
                Debug.Log(exp);
            }
        }


        void Explosion()
        {
            if (Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.J))
            {
                clickCount = (clickCount + 1) % 10;
                ellapsed = 0;
            }
            ellapsed += Time.deltaTime;

            if (ellapsed > toPass && clickCount > 0)
            {

                Debug.Log(clickCount);
                DoExplosion(clickCount);
                clickCount = 0;
            }

        }
    }
}
