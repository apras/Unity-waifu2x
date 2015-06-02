using UnityEngine;
using UnityEditor;
using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using MiniJSON;

public class TextureModifiWaifu2x : AssetPostprocessor
{
	public enum NoiseReductionType
	{
		None = 0,
		Level1 = 1,
		Level2 = 2
	}

	private NoiseReductionType m_useNoiseReductionType;
	private RenderTexture m_dstImage;
	private RenderTexture m_copyImage;
	private RenderTexture m_waifu2xImage;
	private List<Model> m_models;
	private ComputeShader m_csWaifu2x;

	void OnPreprocessTexture()
	{
	}

	void OnPostprocessTexture(Texture2D texture)
	{
		this.m_csWaifu2x = Resources.Load("waifu2x-models/waifu2x") as ComputeShader;
		
		if(this.m_csWaifu2x == null)
		{
			EditorUtility.CompressTexture(texture, texture.format, TextureCompressionQuality.Best);
			return;
		}

		if ((texture.width < Model.BUCKET_WIDHT / 2) || (texture.height < Model.BUCKET_HDEIGHT / 2))
		{
			EditorUtility.CompressTexture(texture, texture.format, TextureCompressionQuality.Best);
			return;
		}

		string _fileName = Path.GetFileNameWithoutExtension(assetPath);

		this.m_useNoiseReductionType = NoiseReductionType.None;
		if(_fileName.Contains("NoiseReduction1"))
		{
			this.m_useNoiseReductionType = NoiseReductionType.Level1;
		}
		else if(_fileName.Contains("NoiseReduction2"))
		{
			this.m_useNoiseReductionType = NoiseReductionType.Level2;
		}

		if(this.m_useNoiseReductionType == NoiseReductionType.None)
		{
			if(!_fileName.EndsWith("Waifu2x"))
			{
				return;
			}
		}

		this.m_copyImage = new RenderTexture(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
		this.m_copyImage.filterMode = FilterMode.Point;
		this.m_copyImage.wrapMode = TextureWrapMode.Clamp;
		this.m_copyImage.enableRandomWrite = true;
		this.m_copyImage.useMipMap = false;
		this.m_copyImage.Create();
		this.copyTexture(texture, ref this.m_copyImage);

		this.m_waifu2xImage = new RenderTexture(texture.width * 2, texture.height * 2, 0, RenderTextureFormat.ARGB32);
		this.m_waifu2xImage.filterMode = FilterMode.Point;
		this.m_waifu2xImage.wrapMode = TextureWrapMode.Clamp;
		this.m_waifu2xImage.enableRandomWrite = true;
		this.m_waifu2xImage.useMipMap = false;
		this.m_waifu2xImage.Create();

		if(this.m_useNoiseReductionType != NoiseReductionType.None)
		{
			this.clearDstImage(texture.width, texture.height);
			this.loadJson("waifu2x-models/noise" + ((int)this.m_useNoiseReductionType).ToString() + "_model");
			this.noiseReduction();
			Graphics.Blit(this.m_dstImage, this.m_copyImage);
			GC.Collect();
		}

		if(_fileName.EndsWith("Waifu2x"))
		{
			this.clearDstImage(this.m_waifu2xImage.width, this.m_waifu2xImage.height);
			this.loadJson("waifu2x-models/scale2.0x_model");
			this.scaling();
			Graphics.Blit(this.m_dstImage, this.m_waifu2xImage);
			GC.Collect();

			texture.Resize(this.m_waifu2xImage.width, this.m_waifu2xImage.height);
			texture.Apply();
			
			RenderTexture.active = this.m_waifu2xImage;
			texture.ReadPixels(new Rect(0, 0, this.m_waifu2xImage.width, this.m_waifu2xImage.height), 0, 0);
			texture.Apply();
			RenderTexture.active = null;
		}
		else
		{
			RenderTexture.active = this.m_copyImage;
			texture.ReadPixels(new Rect(0, 0, this.m_copyImage.width, this.m_copyImage.height), 0, 0);
			texture.Apply();
			RenderTexture.active = null;
		}


		this.disposeTexture(ref this.m_copyImage);
		this.disposeTexture(ref this.m_dstImage);
		this.disposeTexture(ref this.m_waifu2xImage);
			
		GC.Collect();

		EditorUtility.CompressTexture(texture, texture.format, TextureCompressionQuality.Best);
	}

	private void copyTexture(Texture2D src, ref RenderTexture dst)
	{
		int _threadX = src.width / Model.NUM_THREAD_X;
		int _threadY = src.height / Model.NUM_THREAD_Y;

		Color[] _pixels = src.GetPixels();
		int _width = src.width;
		int _height = src.height;

		Vector4[] _pixelData = new Vector4[_pixels.Length];
		for(int _i = 0; _i < _pixels.Length; ++_i)
		{
			_pixelData[_i] = new Vector4(_pixels[_i].r, _pixels[_i].g, _pixels[_i].b, _pixels[_i].a);
		}

		ComputeBuffer _cbPixelData = new ComputeBuffer(_pixelData.Length, Marshal.SizeOf(typeof(Vector4)));
		_cbPixelData.SetData(_pixelData);

		this.m_csWaifu2x.SetBuffer(this.m_csWaifu2x.FindKernel("CSCopyTex"), "SrcTextureData", _cbPixelData);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSCopyTex"), "ResultTexture", dst);
		this.m_csWaifu2x.Dispatch(this.m_csWaifu2x.FindKernel("CSCopyTex"), _threadX, _threadY, 1);

		_cbPixelData.Release();
	}

	private void loadJson(string filePath)
	{
		this.m_models = new List<Model>();
		
		ModelData _modelData = ScriptableObjectUtil.Load<ModelData>("Assets/Resources/" + filePath + ".asset");
		if(_modelData != null)
		{
			foreach(ModelInnerData _innerData in _modelData.InnerData)
			{
				Model _model = new Model();
				_model.InnerData = _innerData;
				this.m_models.Add(_model);
			}
		}
		else
		{
			TextAsset _textAsset = Resources.Load(filePath) as TextAsset;
			string _jsonText = _textAsset.text;
			List<object> _jsonObjects = Json.Deserialize(_jsonText) as List<object>;
			_modelData = ScriptableObject.CreateInstance<ModelData>();
			_modelData.InnerData = new ModelInnerData[_jsonObjects.Count];
			int _count = 0;
			foreach(Dictionary<string, object> _jsonObject in _jsonObjects)
			{
				Model _model = new Model();
				_model.LoadJson(_jsonObject);
				this.m_models.Add(_model);
				_modelData.InnerData[_count++] = _model.InnerData;
			}
			ScriptableObjectUtil.Save<ModelData>("Assets/Resources/" + filePath + ".asset", _modelData);
		}
	}
	
	private void noiseReduction()
	{
		int _width = this.m_copyImage.width;
		int _height = this.m_copyImage.height;
		
		int _bucketCountX = _width / Model.BUCKET_WIDHT;
		int _bucketCountY = _height / Model.BUCKET_HDEIGHT;

		float _p = 0;
		float _pCount = (float)(_bucketCountX * _bucketCountY);
		EditorUtility.DisplayProgressBar("Unity-waifu2x", "Noise Reduction", _p / _pCount);
		for(int _y = 0; _y < _bucketCountY; ++_y)
		{
			int _inOffsetY = Model.PADDING_HDEIGHT;
			int _dstOffsetY = _y * Model.BUCKET_HDEIGHT;
			int _srcOffsetY = _dstOffsetY - _inOffsetY;
			
			if(_y == 0)
			{
				_inOffsetY = 0;
				_srcOffsetY = 0;
			}
			
			if(_y > 1 && _y == _bucketCountY - 1)
			{
				_inOffsetY = Model.PADDING_HDEIGHT * 2;
				_srcOffsetY = _dstOffsetY - _inOffsetY;
			}
			
			for(int _x = 0; _x < _bucketCountX; ++_x)
			{
				int _inOffsetX = Model.PADDING_WIDHT;
				int _dstOffsetX = _x * Model.BUCKET_WIDHT;
				int _srcOffsetX = _dstOffsetX - _inOffsetX;
				
				if(_x == 0)
				{
					_inOffsetX = 0;
					_srcOffsetX = 0;
				}
				
				if(_x > 1 && _x == _bucketCountX - 1)
				{
					_inOffsetX = Model.PADDING_WIDHT * 2;
					_srcOffsetX = _dstOffsetX - _inOffsetX;
				}
				
				this.bucketNoiseReduction(_srcOffsetX, _srcOffsetY, _inOffsetX, _inOffsetY, _dstOffsetX, _dstOffsetY, Model.BUCKET_WIDHT + Model.PADDING_WIDHT * 2, Model.BUCKET_HDEIGHT + Model.PADDING_HDEIGHT * 2, this.m_copyImage);
				_p += 1;
				EditorUtility.DisplayProgressBar("Unity-waifu2x", "Noise Reduction", _p / _pCount);
			}
		}
		EditorUtility.ClearProgressBar();
	}
	
	private void scaling()
	{
		RenderTexture _scaledImage = RenderTexture.GetTemporary(this.m_dstImage.width, this.m_dstImage.height, 0, RenderTextureFormat.ARGB32);
		_scaledImage.filterMode = FilterMode.Bilinear;
		_scaledImage.wrapMode = TextureWrapMode.Clamp;
		_scaledImage.enableRandomWrite = true;
		_scaledImage.useMipMap = false;
		_scaledImage.Create();
		Graphics.Blit(this.m_copyImage, _scaledImage);

		int _bucketCountX = this.m_dstImage.width / Model.BUCKET_WIDHT;
		int _bucketCountY = this.m_dstImage.height / Model.BUCKET_HDEIGHT;

		float _p = 0;
		float _pCount = (float)(_bucketCountX * _bucketCountY);
		EditorUtility.DisplayProgressBar("Unity-waifu2x", "Scaling", _p / _pCount);
		for(int _y = 0; _y < _bucketCountY; ++_y)
		{
			int _inOffsetY = Model.PADDING_HDEIGHT;
			int _dstOffsetY = _y * Model.BUCKET_HDEIGHT;
			int _srcOffsetY = _dstOffsetY - _inOffsetY;
			
			if(_y == 0)
			{
				_inOffsetY = 0;
				_srcOffsetY = 0;
			}
			
			if(_y > 1 && _y == _bucketCountY - 1)
			{
				_inOffsetY = Model.PADDING_HDEIGHT * 2;
				_srcOffsetY = _dstOffsetY - _inOffsetY;
			}
			
			for(int _x = 0; _x < _bucketCountX; ++_x)
			{
				int _inOffsetX = Model.PADDING_WIDHT;
				int _dstOffsetX = _x * Model.BUCKET_WIDHT;
				int _srcOffsetX = _dstOffsetX - _inOffsetX;
				
				if(_x == 0)
				{
					_inOffsetX = 0;
					_srcOffsetX = 0;
				}
				
				if(_x > 1 && _x == _bucketCountX - 1)
				{
					_inOffsetX = Model.PADDING_WIDHT * 2;
					_srcOffsetX = _dstOffsetX - _inOffsetX;
				}
				
				this.bucketScaling(_srcOffsetX, _srcOffsetY, _inOffsetX, _inOffsetY, _dstOffsetX, _dstOffsetY, Model.BUCKET_WIDHT + Model.PADDING_WIDHT * 2, Model.BUCKET_HDEIGHT + Model.PADDING_HDEIGHT * 2, _scaledImage);
				_p += 1;
				EditorUtility.DisplayProgressBar("Unity-waifu2x", "Scaling", _p / _pCount);
			}
		}
		EditorUtility.ClearProgressBar();

		RenderTexture.ReleaseTemporary(_scaledImage);
	}
	
	private void bucketNoiseReduction(int srcOffsetX, int srcOffsetY, int inOffsetX, int inOffsetY, int dstOffsetX, int dstOffsetY, int width, int height, RenderTexture scaledImage)
	{
		int _threadX = width / Model.NUM_THREAD_X + 1;
		int _threadY = height / Model.NUM_THREAD_Y + 1;
		
		RenderTexture _splitY = this.createRenderTextureF1(width, height);
		RenderTexture _splitU = this.createRenderTextureF1(width, height);
		RenderTexture _splitV = this.createRenderTextureF1(width, height);
		
		this.m_csWaifu2x.SetInt("Width2x", width);
		this.m_csWaifu2x.SetInt("Height2x", height);
		
		this.m_csWaifu2x.SetInt("SrcOffsetX", srcOffsetX);
		this.m_csWaifu2x.SetInt("SrcOffsetY", srcOffsetY);
		
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSBucketRgb2Yuv"), "SrcTexture", this.m_copyImage);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSBucketRgb2Yuv"), "SplitY", _splitY);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSBucketRgb2Yuv"), "SplitU", _splitU);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSBucketRgb2Yuv"), "SplitV", _splitV);
		this.m_csWaifu2x.Dispatch(this.m_csWaifu2x.FindKernel("CSBucketRgb2Yuv"), _threadX, _threadY, 1);
		
		RenderTexture _inputPlanes = new RenderTexture(width, height, 0, Model.TEX_FORMAT_F1);
		_inputPlanes.filterMode = FilterMode.Point;
		_inputPlanes.wrapMode = TextureWrapMode.Clamp;
		_inputPlanes.enableRandomWrite = true;
		_inputPlanes.useMipMap = false;
		_inputPlanes.isVolume = true;
		_inputPlanes.volumeDepth = 1;
		_inputPlanes.Create();
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSClearTex"), "OutputPlane", _inputPlanes);
		this.m_csWaifu2x.Dispatch(this.m_csWaifu2x.FindKernel("CSClearTex"), _threadX, _threadY, 1);
		
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSMargeTex"), "SplitY", _splitY);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSMargeTex"), "OutputPlane", _inputPlanes);
		this.m_csWaifu2x.Dispatch(this.m_csWaifu2x.FindKernel("CSMargeTex"), _threadX, _threadY, 1);
		RenderTexture _outputPlanes = null;
		
		for(int _i = 0; _i < this.m_models.Count; ++_i)
		{
			Model _model = this.m_models[_i];
			_model.Filter(this.m_csWaifu2x, width, height, ref _inputPlanes, out _outputPlanes);
			
			if(_i != this.m_models.Count - 1)
			{
				_inputPlanes.DiscardContents();
				_inputPlanes.Release();
				_inputPlanes = _outputPlanes;
				_outputPlanes = null;
			}
		}
		
		RenderTexture _outY = this.createRenderTextureF1(width, height);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSPargeTex"), "SplitY", _outY);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSPargeTex"), "OutputPlane", _outputPlanes);
		this.m_csWaifu2x.Dispatch(this.m_csWaifu2x.FindKernel("CSPargeTex"), _threadX, _threadY, 1);
		
		this.m_csWaifu2x.SetInt("SrcOffsetX", inOffsetX);
		this.m_csWaifu2x.SetInt("SrcOffsetY", inOffsetY);
		
		this.m_csWaifu2x.SetInt("DstOffsetX", dstOffsetX);
		this.m_csWaifu2x.SetInt("DstOffsetY", dstOffsetY);

		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSBucketYuv2Rgb"), "SrcAlpahTexture", scaledImage);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSBucketYuv2Rgb"), "ResultTexture", this.m_dstImage);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSBucketYuv2Rgb"), "SplitY", _outY);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSBucketYuv2Rgb"), "SplitU", _splitU);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSBucketYuv2Rgb"), "SplitV", _splitV);
		this.m_csWaifu2x.Dispatch(this.m_csWaifu2x.FindKernel("CSBucketYuv2Rgb"), (width - Model.PADDING_WIDHT * 2) / Model.NUM_THREAD_X, (height - Model.PADDING_HDEIGHT * 2) / Model.NUM_THREAD_Y, 1);
		
		this.disposeTexture(ref _splitY);
		this.disposeTexture(ref _splitU);
		this.disposeTexture(ref _splitV);
		this.disposeTexture(ref _outY);
		this.disposeTexture(ref _inputPlanes);
		this.disposeTexture(ref _outputPlanes);
	}
	
	private void bucketScaling(int srcOffsetX, int srcOffsetY, int inOffsetX, int inOffsetY, int dstOffsetX, int dstOffsetY, int width, int height, RenderTexture scaledImage)
	{
		int _threadX = width / Model.NUM_THREAD_X + 1;
		int _threadY = height / Model.NUM_THREAD_Y + 1;
		
		RenderTexture _splitY = this.createRenderTextureF1(width, height);
		RenderTexture _splitU = this.createRenderTextureF1(width, height);
		RenderTexture _splitV = this.createRenderTextureF1(width, height);
		
		this.m_csWaifu2x.SetInt("Width2x", width);
		this.m_csWaifu2x.SetInt("Height2x", height);
		
		this.m_csWaifu2x.SetInt("SrcOffsetX", srcOffsetX);
		this.m_csWaifu2x.SetInt("SrcOffsetY", srcOffsetY);

		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSBucketRgb2Yuv"), "SrcTexture", scaledImage);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSBucketRgb2Yuv"), "SplitY", _splitY);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSBucketRgb2Yuv"), "SplitU", _splitU);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSBucketRgb2Yuv"), "SplitV", _splitV);
		this.m_csWaifu2x.Dispatch(this.m_csWaifu2x.FindKernel("CSBucketRgb2Yuv"), _threadX, _threadY, 1);
		
		
		RenderTexture _inputPlanes = new RenderTexture(width, height, 0, Model.TEX_FORMAT_F1);
		_inputPlanes.filterMode = FilterMode.Point;
		_inputPlanes.wrapMode = TextureWrapMode.Clamp;
		_inputPlanes.enableRandomWrite = true;
		_inputPlanes.useMipMap = false;
		_inputPlanes.isVolume = true;
		_inputPlanes.volumeDepth = 1;
		_inputPlanes.Create();
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSClearTex"), "OutputPlane", _inputPlanes);
		this.m_csWaifu2x.Dispatch(this.m_csWaifu2x.FindKernel("CSClearTex"), _threadX, _threadY, 1);
		
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSMargeTex"), "SplitY", _splitY);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSMargeTex"), "OutputPlane", _inputPlanes);
		this.m_csWaifu2x.Dispatch(this.m_csWaifu2x.FindKernel("CSMargeTex"), _threadX, _threadY, 1);
		RenderTexture _outputPlanes = null;
		
		for(int _i = 0; _i < this.m_models.Count; ++_i)
		{
			Model _model = this.m_models[_i];
			_model.Filter(this.m_csWaifu2x, width, height, ref _inputPlanes, out _outputPlanes);
			
			if(_i != this.m_models.Count - 1)
			{
				_inputPlanes.DiscardContents();
				_inputPlanes.Release();
				_inputPlanes = _outputPlanes;
				_outputPlanes = null;
			}
		}
		
		RenderTexture _outY = this.createRenderTextureF1(width, height);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSPargeTex"), "SplitY", _outY);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSPargeTex"), "OutputPlane", _outputPlanes);
		this.m_csWaifu2x.Dispatch(this.m_csWaifu2x.FindKernel("CSPargeTex"), _threadX, _threadY, 1);
		
		this.m_csWaifu2x.SetInt("SrcOffsetX", inOffsetX);
		this.m_csWaifu2x.SetInt("SrcOffsetY", inOffsetY);
		
		this.m_csWaifu2x.SetInt("DstOffsetX", dstOffsetX);
		this.m_csWaifu2x.SetInt("DstOffsetY", dstOffsetY);

		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSBucketYuv2Rgb"), "SrcAlpahTexture", scaledImage);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSBucketYuv2Rgb"), "ResultTexture", this.m_dstImage);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSBucketYuv2Rgb"), "SplitY", _outY);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSBucketYuv2Rgb"), "SplitU", _splitU);
		this.m_csWaifu2x.SetTexture(this.m_csWaifu2x.FindKernel("CSBucketYuv2Rgb"), "SplitV", _splitV);
		this.m_csWaifu2x.Dispatch(this.m_csWaifu2x.FindKernel("CSBucketYuv2Rgb"), (width - Model.PADDING_WIDHT * 2) / Model.NUM_THREAD_X, (height - Model.PADDING_HDEIGHT * 2) / Model.NUM_THREAD_Y, 1);
		
		
		this.disposeTexture(ref _splitY);
		this.disposeTexture(ref _splitU);
		this.disposeTexture(ref _splitV);
		this.disposeTexture(ref _outY);
		this.disposeTexture(ref _inputPlanes);
		this.disposeTexture(ref _outputPlanes);
	}
	
	private RenderTexture createRenderTextureF1(int width, int height)
	{
		RenderTexture _rt = new RenderTexture(width, height, 0, Model.TEX_FORMAT_F1);
		_rt.wrapMode = TextureWrapMode.Clamp;
		_rt.filterMode = FilterMode.Point;
		_rt.enableRandomWrite = true;
		_rt.useMipMap = false;
		_rt.Create();
		
		return _rt;
	}
	
	private RenderTexture createRenderTextureF4(int width, int height)
	{
		RenderTexture _rt = new RenderTexture(width, height, 0, Model.TEX_FORMAT_F4);
		_rt.enableRandomWrite = true;
		_rt.wrapMode = TextureWrapMode.Clamp;
		_rt.filterMode = FilterMode.Point;
		_rt.useMipMap = false;
		_rt.Create();
		
		return _rt;		
	}
	
	private void clearDstImage(int width, int height)
	{
		if(this.m_dstImage != null)
		{
			this.m_dstImage.DiscardContents();
			this.m_dstImage.Release();
		}
		
		this.m_dstImage = new RenderTexture(width, height, 0, Model.TEX_FORMAT_F4);
		this.m_dstImage.filterMode = FilterMode.Point;
		this.m_dstImage.wrapMode = TextureWrapMode.Clamp;
		this.m_dstImage.enableRandomWrite = true;
		this.m_dstImage.useMipMap = false;
		this.m_dstImage.Create();
	}
	
	private void disposeTexture(ref RenderTexture rt)
	{
		rt.DiscardContents();
		rt.Release();
		rt = null;
	}
}
