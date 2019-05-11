#include <GL/glew.h>
#include <GLFW/glfw3.h>
#include <stdlib.h>
#include <glm/glm.hpp>
#include <glm/gtc/matrix_transform.hpp>
#include <iostream>
#include <tuple>
#include <string>
#include <fstream>
#include <sstream>
#include <iostream>
#include <array>
#include <vector>
#include <complex>
#include <random>
#include <math.h>
#include <utility>
#include <algorithm>
#include <boost/hana/tuple.hpp>
#include <boost/hana/zip.hpp>
#include <boost/hana/for_each.hpp>
#include <boost/hana/ext/std/tuple.hpp>
#include <boost/property_tree/ptree.hpp>
#include <boost/property_tree/xml_parser.hpp>
#include "program.hpp"

const std::string g_infoPath   = "../src/config.xml";
const std::string g_vertexPath = "../src/vertex.vs";
const std::string g_fragmentPath = "../src/fragment.fs";

void GLAPIENTRY msgCallback(GLenum source, GLenum type, GLuint id, GLenum severity, GLsizei length, const GLchar* message, const void* userParam )
{
  std::cout << message << '\n';
}

struct screenQuad
{
  GLuint vao, vbo;

  screenQuad()
  {
    static auto verts = std::array<glm::vec4, 6>{
            glm::vec4(1.0f,  1.0f, 0.0f, 0.0f),
            glm::vec4(1.0f, -1.0f, 0.0f, 0.0f),
            glm::vec4(-1.0f,  1.0f, 0.0f, 1.0f),
            glm::vec4(-1.0f, -1.0f, 0.0f, 1.0f),
            glm::vec4(1.0f, -1.0f, 0.0f, 1.0f),
            glm::vec4(-1.0f, 1.0f, 0.0f, 1.0f)
    };

    glGenBuffers(1, &vbo);
    glBindBuffer(GL_ARRAY_BUFFER, vbo);
    glBufferData(GL_ARRAY_BUFFER, sizeof(glm::vec4)*6, verts.data(), GL_STATIC_DRAW);

    glGenVertexArrays(1, &vao);
    glBindVertexArray(vao);

    glVertexAttribPointer(0, 4, GL_FLOAT, GL_FALSE, sizeof(glm::vec4), (void*)0);
    glEnableVertexAttribArray(0);
  }
};

void update()
{

}

int main()
{
  glfwSetErrorCallback([](auto err, const auto* desc){ std::cout << "Error: " << desc << '\n'; });

  // glfw init
  if(!glfwInit())
  {
    std::cout << "glfw failed to initialize\n";
    std::exit(1);
  }

  // context init
  glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 2);
  glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 0);
  auto window = glfwCreateWindow(640, 480, "SDF Rendering", NULL, NULL);
  if (!window)
  {
    std::cout << "window/glcontext failed to initialize\n";
    std::exit(1);
  }

  glfwMakeContextCurrent(window);

  // glew init
  auto err = glewInit();
  if(GLEW_OK != err)
  {
    std::cout << "glew failed to init: " << glewGetErrorString(err) << '\n';
    std::exit(1);
  }

  // gl init
  glEnable(GL_DEBUG_OUTPUT);
  glDebugMessageCallback(msgCallback, 0);
  glEnable(GL_DEPTH_TEST);

  glfwSetKeyCallback(window, [](auto window, auto key, auto scancode, auto action, auto mods){
    if (key == GLFW_KEY_ESCAPE && action == GLFW_PRESS)
      glfwSetWindowShouldClose(window, GLFW_TRUE);
    if(key == GLFW_KEY_F4)
    {
      update();
    }

  });

  // program initialization
  auto quad = screenQuad();

  auto prog = GLDSEL::make_program_from_paths(
    boost::hana::make_tuple(g_vertexPath, g_fragmentPath),
    glDselUniform("orientationMatrix", glm::mat3),
    glDselUniform("transformationMatrix", glm::mat4),
    glDselUniform("origin", glm::vec3),
    glDselUniform("nx", float),
    glDselUniform("ny", float),
    glDselUniform("xlen", float),
    glDselUniform("ylen", float),
    glDselUniform("horizontalPlaneGap", float),
    glDselUniform("verticalPlaneGap", float),
    glDselUniform("projPlaneDist", float),
    glDselUniform("time", float)
  );



  // pre loop declarations/actions
  int width, height;
  float fov = 90.0f;
  glm::mat3 orientationMatrix{};
  glm::mat4 transformationMatrix{};
  glm::vec3 origin{};
  float xlen, ylen, horizd, vertd, proj;
  xlen = -1.0f;
  horizd = 2.0f;

  glfwSwapInterval(1);
  while(!glfwWindowShouldClose(window))
  {
    glfwGetFramebufferSize(window, &width, &height);
    glViewport(0, 0, width, height);

    proj = 1/tan(fov/2);
    ylen = xlen*(float(height)/float(width));
    vertd = -ylen*2;

    transformationMatrix = glm::rotate(glm::mat4(), float(glfwGetTime()), glm::vec3(0,1,0));

    glClearColor(0.1f, 0.1f, 0.1f, 1.0f);
    glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);

    prog.setUniforms(
      glDselArgument("orientationMatrix", orientationMatrix),
      glDselArgument("transformationMatrix", transformationMatrix),
      glDselArgument("origin", origin),
      glDselArgument("nx", float(width)),
      glDselArgument("ny", float(height)),
      glDselArgument("xlen", xlen),
      glDselArgument("ylen", ylen),
      glDselArgument("horizontalPlaneGap", horizd),
      glDselArgument("verticalPlaneGap", vertd),
      glDselArgument("projPlaneDist", proj),
      glDselArgument("time", float(glfwGetTime()))
    );
    glBindVertexArray(quad.vao);
    glDrawArrays(GL_TRIANGLES, 0, 6);

    glfwSwapBuffers(window);
    glfwPollEvents();
  }

  glfwTerminate();
  return 0;
}
