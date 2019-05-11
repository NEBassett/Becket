#version 330 core
out vec4 color;

#define MAX_DIST 600
#define MAX_STEPS 500
#define STEP_COEFFICIENT 1
#define EPSILON 0.001
#define GRADIENT_EPSILON 0.0001
#define REFLECTANCE_STEP 0.2
#define REFLECTANCE_FACTOR 0.2
#define MONTE_CARLO_COEFFICIENT 0.3
#define SPEC_STR 0.5
#define NUM_SAMPLES 6

in vec4 pos;

uniform mat3 orientationMatrix;
uniform vec3 origin;
uniform float nx;
uniform float ny;
uniform float xlen;
uniform float ylen;
uniform float horizontalPlaneGap;
uniform float verticalPlaneGap;
uniform float projPlaneDist;
uniform float time;

struct ray
{
  vec3 origin;
  vec3 dir;
};

ray getRay(in int i, in int j)
{
  ray nRay;
  nRay.origin = origin;
  nRay.dir =
    orientationMatrix[0]*(xlen + horizontalPlaneGap*(i+0.5)/nx) +
    orientationMatrix[1]*(ylen + verticalPlaneGap*(j+0.5)/ny) +
    orientationMatrix[2]*projPlaneDist;
  return nRay;
}

float sphere(in vec3 pos, in vec3 center, float radius)
{
  return distance(pos, center) - radius;
}

float rectangularPrism(in vec3 pos, in vec3 center, in vec3 b)
{
  vec3 d = abs(pos-center) - b;
  return length(max(d,0.0))
         + min(max(d.x,max(d.y,d.z)),0.0); // remove this line for an only partially signed sdf
}

float sdfunion(float a, float b, float k)
{
  float h = clamp( 0.5 + 0.5*(b-a)/k, 0.0, 1.0 );
  return mix(b,a,h) - k*h*(1.0-h);
}

float blend(float a, float b, float t)
{
  return t*a + (1-t)*b;
}

float onion(in float a, in float t)
{
  return abs(a)-t;
}

float extrusion()
{
  return 0.0f;
}

float rand()
{
  return fract(sin(time*10000.0));
}

mat4 rotationMatrix(vec3 axis, float angle)
{
    axis = normalize(axis);
    float s = sin(angle);
    float c = cos(angle);
    float oc = 1.0 - c;

    return mat4(oc * axis.x * axis.x + c,           oc * axis.x * axis.y - axis.z * s,  oc * axis.z * axis.x + axis.y * s,  0.0,
                oc * axis.x * axis.y + axis.z * s,  oc * axis.y * axis.y + c,           oc * axis.y * axis.z - axis.x * s,  0.0,
                oc * axis.z * axis.x - axis.y * s,  oc * axis.y * axis.z + axis.x * s,  oc * axis.z * axis.z + c,           0.0,
                0.0,                                0.0,                                0.0,                                1.0);
}

 vec3 sphericalRotate(in vec3 norm, float u, float v)
 {
   return (rotationMatrix(vec3(1,0,0), u)*rotationMatrix(vec3(0,1,0), v)*vec4(norm,1)).xyz;
 }

float sdf(in vec3 pos)
{
  //return max(-sphere(pos, vec3(0,0,3), abs(sin(time))), sphere(pos, vec3(0.3,1,3.5), 1));
  //return max(-sphere(pos, vec3(0,-2,5), 0.5+1.3*abs(sin(time))), min(rectangularPrism(pos, vec3(0,-3,5), vec3(1.5,0.5,1.5)), rectangularPrism(pos, vec3(0,-1,5), vec3(1.5,0.5,1.5))));
  float fpos = 16;

  return
      min(
        sdfunion(
          sdfunion(
            sdfunion(
              sphere(pos, vec3(0,-2,fpos), 1.5),
              sphere(pos + vec3(sin(time),1.5*cos(time),0), vec3(1,-2,fpos-0.5), 1), 0.5),
            sphere(pos, vec3(0,-2,fpos) + vec3(sin(time)*3,cos(time*1.5)*3,sin(time*2.1)*3), 1),
            0.85
          ),
          sphere(pos, vec3(0,-2,fpos) + vec3(-sin(time)*2,cos(time*1.5)*3,-sin(time*2.1)*3), 1.0),
          0.85), rectangularPrism(pos, vec3(0,-15,5), vec3(70,0.5,70)));
}

bool truth(in vec3 x)
{
  return true;
}

vec3 sdfnorm(in vec3 pos)
{
    return normalize(vec3(
        sdf(vec3(pos.x + GRADIENT_EPSILON, pos.y, pos.z)) - sdf(vec3(pos.x - GRADIENT_EPSILON, pos.y, pos.z)),
        sdf(vec3(pos.x, pos.y + GRADIENT_EPSILON, pos.z)) - sdf(vec3(pos.x, pos.y - GRADIENT_EPSILON, pos.z)),
        sdf(vec3(pos.x, pos.y, pos.z  + GRADIENT_EPSILON)) - sdf(vec3(pos.x, pos.y, pos.z - GRADIENT_EPSILON))
    ));
}

float castsdf(in ray sRay)
{
  float dist = 0.0f;
  int steps = 0;
  while(dist < MAX_DIST && (steps < MAX_STEPS))
  {
    float sdfResult = sdf(sRay.origin + sRay.dir*dist);
    if(sdfResult <= EPSILON)
      return dist;
    dist = dist + sdfResult*STEP_COEFFICIENT;
    steps += 1;
  }
  return 0.0f;
}

vec4 colorFunction(in vec3 pos)
{
  if(pos.y<-10.0)
    return vec4(0.6,0,0.6,1);
  return vec4(0.65,0.6,0.7,1);
}

vec4 calculateLighting(in vec3 lightPos, in vec3 pos)
{
  vec3 lightDir = normalize(lightPos-pos);

  ray lightRay;
  lightRay.origin = lightPos;
  lightRay.dir = -lightDir; // |pos-lightPos|

  float lightResult = castsdf(lightRay);
  float proj = length(lightPos-pos); // project pos onto ray

  if((lightResult + 0.01) >= proj) // pos is visible from lightPos
  {
    vec3 norm = sdfnorm(pos);

    vec3 viewDir = normalize(origin - pos);
    vec3 reflectDir = reflect(-lightDir, norm);

    float spec = pow(max(dot(viewDir, reflectDir), 0.0), 32);
    vec3 specular = vec3(1)*SPEC_STR*spec;

    float diff = max(dot(norm, lightDir), 0.0);

    return colorFunction(pos)*diff+spec;
  } else { // pos is not visible
    return colorFunction(pos)*0.1;
  }
}

vec4 reflectionColor(in vec3 lightPos, in vec3 fromDir, in vec3 pos, in vec3 norm)
{
  vec3 reflected = reflect(fromDir, norm);
  ray lightRay;
  lightRay.origin = pos + reflected*REFLECTANCE_STEP;
  lightRay.dir = reflected;

  //return vec4(reflected,1);
  float lightResult = castsdf(lightRay);

  if(distance(lightResult,0.0) == 0)
  {
    return vec4(0.0);
  }

  vec3 nPos = lightRay.origin + lightRay.dir*lightResult;
  return calculateLighting(lightPos, nPos);
}

vec4 indirectLighting(in vec3 lightPos, in vec3 fromDir, in vec3 pos, in vec3 norm)
{
  vec4 sum = reflectionColor(lightPos, fromDir, pos, norm); // this one is most ideal for clean reflections
  //sum = sum + reflectionColor(lightPos, vec3(0,1,0), pos, norm, REFLECTANCE_FACTOR/2);

  for(int i = 0; i < NUM_SAMPLES*NUM_SAMPLES; i++)
  {
    vec3 sample = sphericalRotate(norm, float(i/NUM_SAMPLES)*6.28, float(i%NUM_SAMPLES)*6.28);
    sum = sum + reflectionColor(lightPos, -sample, pos, norm);
  }

  return MONTE_CARLO_COEFFICIENT*sum/(NUM_SAMPLES*NUM_SAMPLES);
}

void main()
{
  vec3 lightPos = vec3(2,0,0);
  float specStr = 0.5;

  ray fragRay = getRay(int(gl_FragCoord.x), int(gl_FragCoord.y));
  float result = castsdf(fragRay);

  vec3 pos = fragRay.origin + fragRay.dir*result;
  color = indirectLighting(lightPos, fragRay.dir, pos, sdfnorm(pos))+calculateLighting(lightPos, pos);
  // vec3 lightDir = normalize(lightPos-pos);
  //
  // ray lightRay;
  // lightRay.origin = lightPos;
  // lightRay.dir = -lightDir; // |pos-lightPos|
  //
  // float lightResult = castsdf(lightRay);
  // float proj = length(lightPos-pos); // project pos onto ray
  //
  // if((lightResult + 0.01) >= proj) // pos is visible from lightPos
  // {
  //   vec3 norm = sdfnorm(pos);
  //
  //   vec3 viewDir = normalize(origin - pos);
  //   vec3 reflectDir = reflect(-lightDir, norm);
  //
  //   float spec = pow(max(dot(viewDir, reflectDir), 0.0), 32);
  //   vec3 specular = vec3(1)*specStr*spec;
  //
  //   float diff = max(dot(norm, lightDir), 0.0);
  //
  //   color = colorFunction*diff+spec;
  // } else { // pos is not visible
  //   color = colorFunction*0.1;
  // }

  if(result == 0.0)
  {
    color = vec4(0.1,0.1,0.1,1);
  }
}
