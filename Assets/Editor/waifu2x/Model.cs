using UnityEngine;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;

public class Model
{
	public const RenderTextureFormat TEX_FORMAT_F4 = RenderTextureFormat.ARGBHalf;
	public const RenderTextureFormat TEX_FORMAT_F1 = RenderTextureFormat.RHalf;
	public const int NUM_THREAD_X = 4;
	public const int NUM_THREAD_Y = 4;
	public const int BUCKET_WIDHT = 32;
	public const int BUCKET_HDEIGHT = 32;
	public const int PADDING_WIDHT = 8;
	public const int PADDING_HDEIGHT = 8;
	//
	public ModelInnerData InnerData;
	//
	public void LoadJson(Dictionary<string, object> jsonObject)
	{
		this.InnerData = new ModelInnerData();
		
		object _nInputPlane;
		object _nOutputPlanes;
		object _kW;
		object _kH;
		
		jsonObject.TryGetValue("nInputPlane", out _nInputPlane);
		jsonObject.TryGetValue("nOutputPlane", out _nOutputPlanes);
		jsonObject.TryGetValue("kW", out _kW);
		jsonObject.TryGetValue("kH", out _kH);
		this.InnerData.nInputPlanes = Convert.ToInt32(_nInputPlane);
		this.InnerData.nOutputPlanes = Convert.ToInt32(_nOutputPlanes);
		this.InnerData.kernelSize = Convert.ToInt32(_kW);
		//_model.kernelSize = Convert.ToDouble( _kH );
		
		this.InnerData.weights = new List<Matrix4x4>();
		for(int _i = 0; _i < this.InnerData.nInputPlanes * this.InnerData.nOutputPlanes; ++_i)
		{
			this.InnerData.weights.Add(Matrix4x4.zero);
		}
		
		this.InnerData.biases = new List<double>();
		for(int _i = 0; _i < this.InnerData.nOutputPlanes; ++_i)
		{
			this.InnerData.biases.Add(0.0f);
		}
		this.InnerData.nJob = 4;
		
		
		int _matProgress = 0;
		object _wOutputPlaneVObject;
		if(jsonObject.TryGetValue("weight", out _wOutputPlaneVObject))
		{
			List<object> _wOutputPlaneV = _wOutputPlaneVObject as List<object>;
			foreach(object _wInputPlaneVObject in _wOutputPlaneV)
			{
				List<object> _wInputPlaneV = _wInputPlaneVObject as List<object>;
				foreach(object _weightMatVObject in _wInputPlaneV)
				{
					List<object> _weightMatV = _weightMatVObject as List<object>;
					Matrix4x4 _mat = this.InnerData.weights[_matProgress];
					for(int _r = 0; _r < _weightMatV.Count; ++_r)
					{
						List<object> _vs = _weightMatV[_r] as List<object>;
						Vector4 _row = new Vector4(Convert.ToSingle(_vs[0]), Convert.ToSingle(_vs[1]), Convert.ToSingle(_vs[2]), 0);
						_mat.SetRow(_r, _row);
					}
					
					this.InnerData.weights[_matProgress] = _mat;
					_matProgress += 1;
				}
			}
		}
		
		object _biasObjects;
		if(jsonObject.TryGetValue("bias", out _biasObjects))
		{
			List<object> _bs = _biasObjects as List<object>;
			for(int _b = 0; _b < _bs.Count; ++_b)
			{
				this.InnerData.biases[_b] = (double)_bs[_b];
			}
		}
	}
	
	public void Filter(ComputeShader cs, int width, int height, ref RenderTexture inputPlanes, out RenderTexture outputPlanes)
	{
		int _threadX = width / Model.NUM_THREAD_X + 1;
		int _threadY = height / Model.NUM_THREAD_Y + 1;

		outputPlanes = new RenderTexture(width, height, 0, Model.TEX_FORMAT_F1);
		outputPlanes.filterMode = FilterMode.Point;
		outputPlanes.wrapMode = TextureWrapMode.Clamp;
		outputPlanes.enableRandomWrite = true;
		outputPlanes.useMipMap = false;
		outputPlanes.isVolume = true;
		outputPlanes.volumeDepth = this.InnerData.nOutputPlanes;
		outputPlanes.Create();
		cs.SetTexture(cs.FindKernel("CSClearTex"), "OutputPlane", outputPlanes);
		cs.Dispatch(cs.FindKernel("CSClearTex"), _threadX, _threadY, outputPlanes.volumeDepth);

		int _idI = inputPlanes.GetInstanceID();
		int _idO = outputPlanes.GetInstanceID();

		cs.SetInt("InputPlaneCount", this.InnerData.nInputPlanes);
		ComputeBuffer _cbWeight = new ComputeBuffer(this.InnerData.weights.Count, Marshal.SizeOf(typeof(Matrix4x4)));
		_cbWeight.SetData(this.InnerData.weights.ToArray());
		cs.SetBuffer(cs.FindKernel("CSFilter2D"), "Weight", _cbWeight);

		ComputeBuffer _cbBias = new ComputeBuffer(this.InnerData.biases.Count, sizeof(float));
		_cbBias.SetData(this.InnerData.biases.Select(b => Convert.ToSingle(b)).ToArray());
		cs.SetBuffer(cs.FindKernel("CSFilter2D"), "Bias", _cbBias);

		cs.SetTexture(cs.FindKernel("CSFilter2D"), "InputPlane", inputPlanes);
		cs.SetTexture(cs.FindKernel("CSFilter2D"), "OutputPlane", outputPlanes);
		cs.Dispatch(cs.FindKernel("CSFilter2D"), _threadX, _threadY, outputPlanes.volumeDepth);
		_cbWeight.Release();
		_cbBias.Release();


	}
}