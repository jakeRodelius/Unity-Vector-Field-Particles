//By Jake Rodelius

using UnityEngine;

public class ParticleState
{
    private int[] kernels; //states can run multiple kernels at once
    private ComputeShader compShader; //needed for dispatch
    private int groupSize; //needed for dispatch
    private ParticleState nextState;
    
    public ParticleState(ComputeShader computeShaderPointer, ComputeBuffer pBuffer, int gSize, params string[] kernelNames)
    {
        compShader = computeShaderPointer;
        groupSize = gSize;
        kernels = new int[kernelNames.Length];

        for (int i = 0; i < kernelNames.Length; i++) //loop through all the kernel parameters
        {
            kernels[i] = computeShaderPointer.FindKernel(kernelNames[i]); //save the int for each kernel
            computeShaderPointer.SetBuffer(kernels[i], "particleBuffer", pBuffer); //set the particle buffer as the kernels buffer
        }
    }

    public void Update()
    {
        foreach(int k in kernels) //dispatch all kernels
        {
            compShader.Dispatch(k, groupSize, 1, 1);
        }
    }

    public void SetNextState(ParticleState newNextState)
    {
        nextState = newNextState;
    }

    public ParticleState GetNextState()
    {
        return nextState;
    }
}


public class VectorFieldParticles : MonoBehaviour
{
    public struct Particle //this struct needs to match in the compute shader and pixel shader
    {
        public Vector3 position;
        public Vector3 initialPosition;
        public float life;
    }
    private const int particleStructSize = 28; //the size of the Particle struct: 4*(3+3+1)

    //Shader References
    [Header("Shader References")]
    [SerializeField]
    private ComputeShader computeShader;
    [SerializeField]
    private Material particleMaterial;

    //Particles
    [Header("Particle System Parameters")]
    [SerializeField]
    private int particleCount;
    [SerializeField]
    private float particleLifetime = 5.0f; //how long in seconds particles should persist
    [SerializeField]
    private float repelRadius = 2.0f; //how wide a radius the particles repel from when you click
    [SerializeField]
    private float repelPower = 2.0f; //how wide a radius the particles repel from when you click
    [SerializeField]
    private Vector3 vectorFieldDimensions; //particles spawn within this box
    private Vector3 vectorFieldExtents; //half the size of the box

    //Compute Shader Variables
    private const int groupSize = 256; //number of threads defined above the kernels in the compute shader
    private int groupCount; //how many thread groups needed for all particles
    private ComputeBuffer particleBuffer;

    //State Machine
    private ParticleState spiral, eyes, opticalIllusion, combination;

    private ParticleState currentState;

    //Kernels outside of states
    private int lifetimeKernel;
    private int repelKernel;
    
    void Start()
    {
        //Do some math
        vectorFieldExtents = vectorFieldDimensions * 0.5f;

        groupCount = (particleCount / groupSize)
            + Mathf.Max(particleCount % groupSize, 1); //add an extra group if there's any remainder

        //Create the buffer of particles
        particleBuffer = new ComputeBuffer(particleCount, particleStructSize);
        InitializeParticles();

        //Send the buffer and variable information to the shaders
        particleMaterial.SetBuffer("particleBuffer", particleBuffer);
        particleMaterial.SetFloat("_Lifetime", particleLifetime);
        computeShader.SetFloat("particleLifetime", particleLifetime);
        computeShader.SetFloat("repelRadius", repelRadius);
        computeShader.SetFloat("repelPower", repelPower);

        //Set up global kernels
        lifetimeKernel = computeShader.FindKernel("CSLifetime");
        computeShader.SetBuffer(lifetimeKernel, "particleBuffer", particleBuffer);
        repelKernel = computeShader.FindKernel("CSRepel");
        computeShader.SetBuffer(repelKernel, "particleBuffer", particleBuffer);

        //Create states
        spiral          = new ParticleState(computeShader, particleBuffer, groupSize, "CSSpiral");
        eyes            = new ParticleState(computeShader, particleBuffer, groupSize, "CSEyes");
        opticalIllusion = new ParticleState(computeShader, particleBuffer, groupSize, "CSOpticalIllusion");
        combination     = new ParticleState(computeShader, particleBuffer, groupSize, "CSOpticalIllusion", "CSSpiral");
        spiral         .SetNextState(eyes);
        eyes           .SetNextState(opticalIllusion);
        opticalIllusion.SetNextState(combination);
        combination    .SetNextState(spiral);

        //Set initial state
        currentState = spiral;
    }
    
    private void InitializeParticles()
    {
        Particle[] particleArray = new Particle[particleCount];
        for (int i = 0; i < particleCount; i++)
        {
            particleArray[i].initialPosition.Set( //initial position is random within the box defined in editor
                Random.Range(-vectorFieldExtents.x, vectorFieldExtents.x),
                Random.Range(-vectorFieldExtents.y, vectorFieldExtents.y),
                Random.Range(-vectorFieldExtents.z, vectorFieldExtents.z)
                );
            particleArray[i].position = particleArray[i].initialPosition; //starts at initialposition
            particleArray[i].life = Random.value * particleLifetime; //starts with random age
        }
        particleBuffer.SetData(particleArray);
    }

    //Variables used in Update
    private float startTime = 0.0f;
    private float stateDuration = 30.0f;
    void Update()
    {
        //Change state based on time
        if(Time.time - startTime > stateDuration) //changes the state every [state duration]
        {
            currentState = currentState.GetNextState();
            startTime = Time.time;
        }

        //Create a repelling force where the mouse is clicked
        if (Input.GetMouseButton(0))
        {
            Vector3 clickedPosition = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z)); //uses camera z so this vector has a Z of 0
            computeShader.SetVector("repelPosition", clickedPosition);
            computeShader.Dispatch(repelKernel, groupCount, 1, 1);
        }

        //Send values to shader and dispatch shader kernels
        computeShader.SetFloat("deltaTime", Time.deltaTime); //send the CS how much time has passed since last dispatch
        computeShader.Dispatch(lifetimeKernel, groupCount, 1, 1);
        currentState.Update();
    }

    //A special unity function for procedural drawing
    private void OnRenderObject()
    {
        particleMaterial.SetPass(0); //send the buffer to the material
        Graphics.DrawProcedural(MeshTopology.Points, 1, particleCount);
    }

    void OnDisable()
    {
        if (particleBuffer != null)
            particleBuffer.Release(); //Just a safe thing to do I saw in some examples
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(Vector3.zero, vectorFieldDimensions); //show the area where the particles spawn in editor
    }
}
