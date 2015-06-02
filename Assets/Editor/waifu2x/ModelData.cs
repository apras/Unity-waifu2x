using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class ModelInnerData
{
	public int nInputPlanes;
	public int nOutputPlanes;
	public List<Matrix4x4> weights;
	public List<double> biases;
	public int kernelSize;
	public int nJob;
}

public class ModelData : ScriptableObject
{
	public ModelInnerData[] InnerData;
}