
__kernel void square_array(__global  float * array)
{
	int id = get_global_id(0);
	array[id] = array[id] * array[id];
}