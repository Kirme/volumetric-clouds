import os
import glob
import pathlib
import csv
import matplotlib.pyplot as plt
import numpy as np
from skimage import metrics
import cv2

defFPS = [] # List of default FPS without optimization

diff = [0, 0, 0]
thresholds = [2.0, 4.0, 8.0]

base = ''

def get_diff(dir, it):
    extension = 'csv'
    os.chdir(dir)
    result = glob.glob('*.{}'.format(extension))
    
    eval = result[0]

    currentDiff = 0

    with open(eval, newline='') as csvfile:
        reader = csv.reader(csvfile, delimiter=',', quotechar='|')
        next(reader)
        i = 0
        for row in reader:
            currentDiff += float(row[2]) - defFPS[i]
            i += 1

        currentDiff /= i

    diff[it] += currentDiff


def get_def(dir):
    extension = 'csv'
    os.chdir(dir)
    result = glob.glob('*.{}'.format(extension))
    
    eval = result[0]

    with open(eval, newline='') as csvfile:
        reader = csv.reader(csvfile, delimiter=',', quotechar='|')
        next(reader)
        for row in reader:
            defFPS.append(float(row[2]))
    
def fps_reset():
    defFPS.clear()

def fps_walk(root):
    it = 0
    for subdir, dirs, files in os.walk(root):
        for dir in dirs:
            if (dir != 'img'):
                vol, cam, threshold = dir.split('_')
                if (float(threshold) == 0.0):
                    fps_reset()
                    it = 0
                    get_def(root + '/' + dir)
                else:
                    get_diff(root + '/' + dir, it)
                    it += 1

def fps_result():
    actualDiff = [x / 4 for x in diff]
    print(actualDiff)

    plt.plot(thresholds, actualDiff)
    plt.xlabel('Thresholds')
    plt.ylabel('FPS difference')
    plt.title('Engine FPS based on pixel coherence threshold')

    plt.xticks(np.arange(0.2, 1.2, 0.2))

    plt.savefig('engine_fps.png')

# SSIM

ssim = [0.0, 0.0, 0.0]
def_pos = "1"

def ssim_diff(dir, def_dir, it):
    image = cv2.imread(dir)
    def_image = cv2.imread(def_dir)

    sim = metrics.structural_similarity(def_image, image, multichannel=True)
    
    ssim[it] += sim
 
def ssim_walk(root):
    def_dir = ''
    for subdir, dirs, files in os.walk(root):
        for file in files:
            if (dir != 'img'):
                sseed, dseed, interp, pos = file.split('_')
                img_dir = root + '/' + file

                if (pos != def_pos + ".png"):
                    continue
                
                if (float(interp) == 0.0):
                    it = 0
                    def_dir = img_dir
                else:
                    ssim_diff(img_dir, def_dir, it)
                    it += 1

def ssim_result():
    actual_ssim = [x / 4 for x in ssim]

    plt.plot(thresholds, ssim)
    plt.xlabel('nth interpolated')
    plt.ylabel('SSIM Value')
    plt.title('SSIM')
    
    #plt.xticks(np.arange(0.2, 1.2, 0.2))
    #plt.yticks(np.arange(0.0, 1.0, 0.2))

    plt.savefig(str(pathlib.Path().resolve()) + '/graphs/pos-' + def_pos + '_ssim.png')

def print_single_ssim(first, second, root):
    first = cv2.imread(root + "/" + first)
    second = cv2.imread(root + "/" + second)

    print(metrics.structural_similarity(cv2.imread(first), cv2.imread(second), multichannel=True))

def main():
    root = str(pathlib.Path().resolve()) + '/img'

    # Get graph of FPS vs coherence threshold
    #fps_walk(root)
    #fps_result()

    ssim_walk(root)
    ssim_result()

if __name__ == "__main__":
    main()