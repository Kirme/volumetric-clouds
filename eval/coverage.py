import os
import glob
import pathlib
import csv
import matplotlib.pyplot as plt
import numpy as np
from skimage import metrics
import cv2
import statistics
import re

values = {}
num_seeds = 0

def get_stddev():
    stdev = []
    
    for key in values:
        stdev.append(statistics.stdev(values[key]))

    return stdev

fps = [0, 0, 0, 0, 0]
coverages = [0.1, 0.2, 0.3, 0.4, 0.5]

def get_fps(file, it):
    with open(file, newline='') as csvfile:
        reader = csv.reader(csvfile, delimiter=',', quotechar='|')
        
        sum = 0
        amount = 0
        print("------")
        for row in reader:
            if not it in values:
                values[it] = []

            values[it].append(int(row[0]))
            sum += int(row[0])
            print(int(row[0]))
            amount += 1

        fps[it] += sum / amount

def fps_walk(root):
    for subdir, dirs, files in os.walk(root):
        for file in files:
            thresh, _, _ = re.split('_|.txt', file)
            loc = root + '/' + file

            index = float(thresh) / 0.1

            index = int(round(index) - 1)
            get_fps(loc, index)

def fps_result():
    actual_fps = [x / 4 for x in fps]

    stdev = get_stddev()

    print(actual_fps)

    plt.errorbar(coverages, actual_fps, stdev)
    plt.plot(coverages, actual_fps)
    plt.xlabel('Coverage Threshold')
    plt.ylabel('FPS difference')
    plt.title('FPS based on coverage')

    plt.savefig('fpscov.png')

# SSIM
ssim = [0.0, 0.0, 0.0]
def_cov = "0.1"
thresholds = [2.0, 4.0, 8.0]

def ssim_diff(dir, def_dir, it):
    image = cv2.imread(dir)
    def_image = cv2.imread(def_dir)

    sim = metrics.structural_similarity(def_image, image, multichannel=True)
    ssim[it] += sim

    if not it in values:
        values[it] = []

    values[it].append(sim)
 
def ssim_walk(root):
    global num_seeds
    def_dir = ''

    for subdir, dirs, files in os.walk(root):
        for file in files:
            if (dir != 'img'):
                pos, interp, thresh, _ = re.split('_|.png', file)
                img_dir = root + '/' + file

                if (thresh != def_cov):
                    continue
                
                if (float(interp) == 0.0):
                    it = 0
                    def_dir = img_dir
                    num_seeds += 1
                else:
                    ssim_diff(img_dir, def_dir, it)
                    it += 1
         

def ssim_result():
    actual_ssim = ssim

    stdev = get_stddev()

    if (num_seeds != 0):
        actual_ssim = [x / num_seeds for x in ssim]

    print(actual_ssim)

    plt.errorbar(thresholds, actual_ssim, stdev)
    plt.plot(thresholds, actual_ssim)
    plt.xlabel('nth ray marched')
    plt.ylabel('SSIM Value')
    plt.title('SSIM')

    plt.savefig(str(pathlib.Path().resolve()) + '/graphs/coverage/thresh-' + def_cov + '_ssim.png')

def print_single_ssim(first, second, root):
    first = cv2.imread(root + "/" + first)
    second = cv2.imread(root + "/" + second)

    print(metrics.structural_similarity(cv2.imread(first), cv2.imread(second), multichannel=True))

def ssim_reset(new_cov):
    global values, ssim, num_seeds, def_cov

    values = {}
    ssim = [0.0, 0.0, 0.0]
    num_seeds = 0
    def_cov = new_cov
    plt.cla()

def main():
    root = str(pathlib.Path().resolve())

    # Get graph of FPS vs n
    #fps_walk(root + '/coverage/fps')
    #fps_result()

    cov = 0.1

    ssim_reset(str(cov))
    ssim_walk(root + '/coverage/img')
    ssim_result()

if __name__ == "__main__":
    main()