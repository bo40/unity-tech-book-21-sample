// CPython を埋め込み、Unity から generator ベースのボスを 1 フレームずつ進めるための薄い C 層。
// Python 側には組み込みモジュール unibridge（spawn、hp、aim）を見せる。
#define PY_SSIZE_T_CLEAN
#include <Python.h>
#include <stdint.h>
#include <string.h>

#include "../common/unibridge.h"

static UniBridge bridge;
static PyObject *boss_generator;
static long frames_to_wait;
static char last_error[4096];

static void capture_python_error(void) {
    PyObject *exception = PyErr_GetRaisedException();
    PyObject *text = exception ? PyObject_Repr(exception) : NULL;
    const char *utf8 = text ? PyUnicode_AsUTF8(text) : "unknown Python error";
    snprintf(last_error, sizeof(last_error), "%s", utf8 ? utf8 : "unprintable Python error");
    Py_XDECREF(text);
    Py_XDECREF(exception);
    PyErr_Clear();
}

static uint32_t to_channel(double value) {
    if (value < 0.0) value = 0.0;
    if (value > 1.0) value = 1.0;
    return (uint32_t)(value * 255.0 + 0.5);
}

// unibridge.spawn(angle, speed, accel=0.0, rgb=(1.0, 1.0, 1.0), x=0.0, y=0.0)
static PyObject *unibridge_spawn(PyObject *self, PyObject *args, PyObject *kwargs) {
    static char *keywords[] = {"angle", "speed", "accel", "rgb", "x", "y", NULL};
    double angle, speed, accel = 0.0, red = 1.0, green = 1.0, blue = 1.0, x = 0.0, y = 0.0;
    if (!PyArg_ParseTupleAndKeywords(args, kwargs, "dd|d(ddd)dd", keywords,
                                     &angle, &speed, &accel, &red, &green, &blue, &x, &y))
        return NULL;
    uint32_t rgba = to_channel(red) << 24 | to_channel(green) << 16 | to_channel(blue) << 8 | 0xFF;
    bridge.spawn((float)x, (float)y, (float)angle, (float)speed, (float)accel, rgba, bridge.user);
    Py_RETURN_NONE;
}

static PyObject *unibridge_hp(PyObject *self, PyObject *unused) {
    return PyFloat_FromDouble(bridge.hp_ratio(bridge.user));
}

static PyObject *unibridge_aim(PyObject *self, PyObject *unused) {
    return PyFloat_FromDouble(bridge.aim_angle(bridge.user));
}

static PyMethodDef unibridge_methods[] = {
    {"spawn", (PyCFunction)(void (*)(void))unibridge_spawn, METH_VARARGS | METH_KEYWORDS, "Spawn a bullet."},
    {"hp", unibridge_hp, METH_NOARGS, "Boss HP ratio (1.0 to 0.0)."},
    {"aim", unibridge_aim, METH_NOARGS, "Angle from the boss to the player."},
    {NULL, NULL, 0, NULL},
};

static struct PyModuleDef unibridge_module = {
    PyModuleDef_HEAD_INIT, "unibridge", NULL, -1, unibridge_methods,
};

static PyObject *init_unibridge(void) { return PyModule_Create(&unibridge_module); }

// home: lib/python3.14 を含むディレクトリ。0 なら成功。2 回目以降は何もしない
int pybridge_init(const char *home) {
    if (Py_IsInitialized())
        return 0;
    if (PyImport_AppendInittab("unibridge", init_unibridge) < 0) {
        snprintf(last_error, sizeof(last_error), "PyImport_AppendInittab failed");
        return -1;
    }
    PyConfig config;
    PyConfig_InitIsolatedConfig(&config);
    // シグナルは Unity（と IL2CPP の NullReference 検出）が使うので、Python には触らせない
    config.install_signal_handlers = 0;
    PyStatus status = PyConfig_SetBytesString(&config, &config.home, home);
    if (!PyStatus_Exception(status))
        status = Py_InitializeFromConfig(&config);
    PyConfig_Clear(&config);
    if (PyStatus_Exception(status)) {
        snprintf(last_error, sizeof(last_error), "Py_InitializeFromConfig: %s",
                 status.err_msg ? status.err_msg : "(no message)");
        return -1;
    }
    return 0;
}

// source を __main__ で実行し、boss(unibridge) を呼んで generator を作る
int pybridge_load(const char *source, UniBridge host) {
    bridge = host;
    PyObject *globals = PyModule_GetDict(PyImport_AddModule("__main__"));
    PyObject *result = PyRun_String(source, Py_file_input, globals, globals);
    if (!result) {
        capture_python_error();
        return -1;
    }
    Py_DECREF(result);
    PyObject *boss = PyDict_GetItemString(globals, "boss");
    PyObject *api = PyImport_ImportModule("unibridge");
    if (!boss || !api) {
        if (PyErr_Occurred()) capture_python_error();
        else snprintf(last_error, sizeof(last_error), "boss() is not defined");
        Py_XDECREF(api);
        return -1;
    }
    Py_XDECREF(boss_generator);
    boss_generator = PyObject_CallOneArg(boss, api);
    Py_DECREF(api);
    frames_to_wait = 0;
    if (!boss_generator) {
        capture_python_error();
        return -1;
    }
    return 0;
}

// 1 フレーム進める。0: 継続中、1: 終了、-1: エラー
int pybridge_step(void) {
    if (!boss_generator)
        return 1;
    if (frames_to_wait > 0) {
        frames_to_wait--;
        return 0;
    }
    PyObject *yielded = PyIter_Next(boss_generator);
    if (!yielded) {
        Py_CLEAR(boss_generator);
        if (PyErr_Occurred()) {
            capture_python_error();
            return -1;
        }
        return 1;
    }
    // yield n で n フレーム待つ（このフレームを含む）
    long frames = PyLong_AsLong(yielded);
    Py_DECREF(yielded);
    if (frames == -1 && PyErr_Occurred()) {
        capture_python_error();
        return -1;
    }
    frames_to_wait = frames > 0 ? frames - 1 : 0;
    return 0;
}

// Python のバージョン文字列（動作確認用）
const char *pybridge_version(void) { return Py_GetVersion(); }

const char *pybridge_last_error(void) { return last_error; }
