inline float luminance(uint4 bgra)
{
  float4 bgrafloat = convert_float4(bgra) / 255.0f; //Convert to normalized [0..1] float
  //Convert RGB to luminance (make the image grayscale).
  float lum =  sqrt(0.241f * bgrafloat.z * bgrafloat.z + 0.691f * 
                      bgrafloat.y * bgrafloat.y + 0.068f * bgrafloat.x * bgrafloat.x);

  return lum;
}

__kernel void sobelEdgeDetect(__read_only  image2d_t srcImg, __write_only image2d_t dstImg, float threshold)
{
  const sampler_t smp = CLK_NORMALIZED_COORDS_FALSE | //Natural coordinates
    CLK_ADDRESS_CLAMP_TO_EDGE | //Clamp to zeros
    CLK_FILTER_LINEAR;
  //int2 coord = (int2)(get_global_id(0), get_global_id(1));
  int x = get_global_id(0);
  int y = get_global_id(1);
  uint4 bgra = read_imageui(srcImg, smp, (int2)(x,y)); //The byte order is BGRA

  float lum_nw = luminance(read_imageui(srcImg, smp, (int2)(x-1, y-1)));
  float lum_n  = luminance(read_imageui(srcImg, smp, (int2)(x,   y-1)));
  float lum_ne = luminance(read_imageui(srcImg, smp, (int2)(x+1, y-1)));
  float lum_w  = luminance(read_imageui(srcImg, smp, (int2)(x-1, y  )));
  float lum    = luminance(bgra);
  float lum_e  = luminance(read_imageui(srcImg, smp, (int2)(x+1, y  )));
  float lum_sw = luminance(read_imageui(srcImg, smp, (int2)(x-1, y+1)));
  float lum_s  = luminance(read_imageui(srcImg, smp, (int2)(x,   y+1)));
  float lum_se = luminance(read_imageui(srcImg, smp, (int2)(x+1, y+1)));

  float Gx = -1 * lum_nw + lum_ne + -2 * lum_w + 2 * lum_e + -1 * lum_sw + lum_se;
  float Gy = lum_nw + 2 * lum_n  +  lum_ne + -1 * lum_sw + -2 * lum_s + -1 * lum_se;
  float G = sqrt(Gx*Gx + Gy*Gy);

  if(G < threshold)
  {
	//bgra.x = bgra.y = bgra.z = (uint) (lum * 255.0f);
	bgra.x = bgra.y = bgra.z = bgra.w = 255;
  }
  else
  {
	bgra.x = bgra.y = bgra.z = 0;
	bgra.w = 255;
  }

  write_imageui(dstImg, (int2)(x,y), bgra);
}
