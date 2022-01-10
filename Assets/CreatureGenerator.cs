using UnityEngine;
using System.Collections.Generic;
using System;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using ew;
using Unity.Rendering;

namespace BGE.Forms
{
    public class CreatureGenerator : MonoBehaviour {

        public Transform parentSegmentsTo = null;
        public bool makeRotator = false;

        public bool scaleFins = true;

        // Changes the position of the fins
        public float finRotatorOffset = 0.0f;

        public float partOffset = 0.0f;

        [Range(0.0f, Mathf.PI * 2.0f)]
        public float theta = 0.1f;

        [Range(0.0f, 10.0f)]
        public float frequency = 1.0f;


        [Range(1, 1000)]
        public int numParts = 5;

        [Range(-1000.0f, 1000.0f)]
        public float gap = 1;

        public Color color = Color.blue;
        public bool assignColors = true;

        [Range(1.0f, 5000.0f)]
        public float verticalSize = 1.0f;

        public bool flatten = false;

        public Mesh mesh;

        public EntityArchetype headArchitype;
        public EntityArchetype bodyArchitype;

        public EntityArchetype tailArchitype;

        public RenderMesh dodRenderMesh;
        

        public GameObject tailPrefab;
        public GameObject leftFinPrefab;
        public GameObject rightFinPrefab;
        public GameObject seatPrefab;

        public float finRotationOffset = 20.0f;

        public string finList;

        public float lengthVariation = 0;

        Dictionary<string, Entity> bodyParts = new Dictionary<string, Entity>();

        public int spineLength;

        Entity GetCreaturePart(string key, EntityArchetype archetype, int index, int boidId)
        {
            if (bodyParts.ContainsKey(key))
            {
                return bodyParts[key];
            }
            else
            {
                Entity part = entityManager.CreateEntity(archetype);
                entityManager.AddSharedComponentData(part, dodRenderMesh);
                if (index == 0)
                {
                    entityManager.SetComponentData(part, new Boid() { boidId = boidId, mass = 1, maxSpeed = 100 * UnityEngine.Random.Range(0.9f, 1.1f), maxForce = 400, weight = 200 });
                    entityManager.SetComponentData(part, new Wander()
                        {
                            distance = 2
                            ,
                            radius = 1.2f,
                            jitter = 80,
                            target = UnityEngine.Random.insideUnitSphere * 1.2f
                        });

                }
                //GameObject part = GameObject.Instantiate<GameObject>(prefab);

                //if (!part.GetComponent<Renderer>().material.name.Contains("Trans"))
                //{
                //    part.GetComponent<Renderer>().material.color = Color.black;
                //}
                bodyParts[key] = part;
                return part;
            }
        }
    
        public void OnDrawGizmos()
        {
            if (!Application.isPlaying)
            {
                List<CreaturePart> creatureParts = CreateCreatureParams();
                Gizmos.color = Color.yellow;
                foreach (CreaturePart cp in creatureParts)
                {
                    Gizmos.DrawWireSphere(cp.position, cp.size * 0.5f);
                }
                LogParts(creatureParts);
            }            
        }

        void LogParts(List<CreaturePart> creatureParts)
        {
            string cps = "";
            foreach (CreaturePart cp in creatureParts)
            {
                cps += cp;
            }
        }

        EntityManager entityManager;
        NativeArray<Entity> allTheBoids;
        NativeArray<Entity> allTheSpines;

        public void CreateCreature(int boidId, ref EntityManager entityManager, ref NativeArray<Entity> allTheBoids, ref NativeArray<Entity> allTheSpines)
        {
            this.entityManager = entityManager;
            this.allTheBoids = allTheBoids;
            this.allTheSpines = allTheSpines;
            string[] fla = finList.Split(',');
            List<CreaturePart> creatureParts = CreateCreatureParams();
            Gizmos.color = Color.yellow;

            int finNumber = 0;
            for (int i = 0; i < creatureParts.Count; i ++)
            {
                CreaturePart cp = creatureParts[i];
                Entity part = GetCreaturePart("body part " + i, cp.archetype, i, boidId);

                Translation p = new Translation
                {
                    Value = cp.position
                };

                int spineIndex = (boidId * spineLength + 1) + i;
                allTheSpines[spineIndex] = part;
                int parentId = spineIndex - 1;
                
            
                if (i == 0)
                {
                    allTheBoids[boidId] = part;
                    entityManager.SetComponentData(part, new Spine() { parent = -1, spineId = spineIndex });
                    //boid = part.GetComponent<Boid>();
                }
                else
                {                    
                    Vector3 offs = creatureParts[i].position - creatureParts[i-1].position;
                    Debug.Log(offs);
                    entityManager.SetComponentData(part, new Spine() { parent = parentId, spineId = spineIndex, offset = offs });
                }

                Rotation r = new Rotation
                {
                    Value = Quaternion.identity
                };

                entityManager.SetComponentData(part, p);
                entityManager.SetComponentData(part, r);

                NonUniformScale s = new NonUniformScale
                {
                    Value = new Vector3(cp.size, cp.size, cp.size)
                };

                entityManager.SetComponentData(part, s);

                entityManager.AddSharedComponentData(part, dodRenderMesh);

                
                /*
                // Make fins if required            
                if (System.Array.Find(fla, p => p == "" + i) != null)
                {
                    float scale = cp.size / ((finNumber / 2) + 1);
                    //GameObject leftFin = GenerateFin(scale, cp, boid, (finNumber * finRotationOffset), part, FinAnimator.Side.left, finNumber);
                    //GameObject rightFin = GenerateFin(scale, cp, boid, (finNumber * finRotationOffset), part, FinAnimator.Side.right, finNumber);
                    finNumber++;
                }
                */
            }
        }

        private GameObject GenerateFin(float scale, CreaturePart cp, int boid, float rotationOffset, GameObject part, FinAnimator.Side side, int finNumber)
        {
            GameObject fin = null; 
            Vector3 pos = cp.position;
            switch (side)
            {
                case FinAnimator.Side.left:
                    //fin = GetCreaturePart("left fin" + finNumber, leftFinPrefab);
                    pos -= (transform.right * cp.size / 2);                
                    break;
                case FinAnimator.Side.right:
                    //fin = GetCreaturePart("right fin" + finNumber, rightFinPrefab);
                    pos += (transform.right * cp.size / 2);
                    break;
            }
            fin.transform.position = pos;
            fin.transform.rotation = fin.transform.rotation * transform.rotation;
            if (scaleFins)
            {
                fin.transform.localScale = new Vector3(scale, scale, scale);
            }
            fin.GetComponentInChildren<FinAnimator>().boid = boid;
            //fin.GetComponentInChildren<FinAnimator>().side = side;
            fin.GetComponentInChildren<FinAnimator>().rotationOffset -= rotationOffset;
            fin.transform.parent = part.transform;
        
            return fin;
        }

        public int seatPosition = 5;


        List<CreaturePart> CreateCreatureParams()
        {
            List<CreaturePart> cps = new List<CreaturePart>();
            float thetaInc = (Mathf.PI * frequency) / (numParts);
            float theta = this.theta;
            float lastPartSize = 0;
            Vector3 pos = Vector3.zero;

            int half = (numParts / 2) - 1; 

            for (int i = 0; i < numParts; i++)
            {
                float partSize = 0;
                if (makeRotator && i == 0)
                {
                    partSize = 0.03f;
                }
                else
                {
                    partSize = verticalSize * Mathf.Abs(Mathf.Sin(theta)); // Never used! + (verticalSize * lengthVariation * UnityEngine.Random.Range(0.0f, 1.0f));
                    theta += thetaInc;
                }
                pos -= ((((lastPartSize + partSize) / 2.0f) + gap) * Vector3.forward);
                if (flatten)
                {
                    pos.y -= (partSize / 2);
                }
                lastPartSize = partSize;
                if (i == seatPosition && seatPrefab != null)
                {
                    /*
                    cps.Add(new CreaturePart(pos
                        , partSize
                        , CreaturePart.Part.seat
                        , seatPrefab
                        , Quaternion.identity));
                        */
                }
                else
                {
                    cps.Add(new CreaturePart(pos
                        , partSize
                        , (i == 0) ? CreaturePart.Part.head : (i < numParts - 1) ? CreaturePart.Part.body : CreaturePart.Part.tail
                        , (i == 0) ? headArchitype : (i < numParts - 1) ? bodyArchitype : (tailPrefab != null) ? tailArchitype : bodyArchitype
                        , Quaternion.identity));

                }

            }
            return cps;
        }

        void Start()
        {
            Utilities.SetLayerRecursively(this.gameObject, this.gameObject.layer);
            if (assignColors)
            {
                Utilities.RecursiveSetColor(this.gameObject, color);
            }
        }
	
        // Update is called once per frame
        void Update () {
	
        }
    }

    struct CreaturePart
    {
        public Vector3 position;
        public Quaternion rotation;
        public float size;
        public enum Part { head, body, fin , tail, tenticle, seat};
        public Part part;
        public EntityArchetype archetype;
    
        public CreaturePart(Vector3 position, float scale, Part part, EntityArchetype archetype, Quaternion rotation)
        {
            this.position = position;
            this.size = scale;
            this.part = part;
            this.archetype = archetype;
            this.rotation = rotation;
        }

        public override string ToString()
        {
            return position + ", " + size + ", " + rotation;
        }
    }
}